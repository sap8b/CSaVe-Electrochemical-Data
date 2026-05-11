using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Fits a Butler-Volmer model to a merged polarization curve using a dynamic parameter vector
    /// that only includes the reactions selected for the analysis run.
    /// Reactions are stored in a list sorted ascending by equilibrium potential (Nernst), so the
    /// algorithm is independent of the number and type of reactions — any factory added to the
    /// default constructor list is automatically included.
    /// </summary>
    public sealed class BvCurveFitter : IBvCurveFitter
    {
        private const double DefaultPh = 8.0;
        private const double DefaultMetalIonConcentrationM = 1.0e-6;
        private const double TafelLowerOffsetV = 0.01;
        private const double TafelUpperOffsetV = 0.15;
        private const double DefaultBeta = 0.5;
        private const double DefaultI0MetalAcm2 = 1e-8;
        private const double DefaultI0OrrAcm2 = 1e-8;
        private const double DefaultI0HerAcm2 = 1e-9;
        private const double DefaultIlimOrrAcm2 = 1e-6;
        private const double IlimDepthFraction = 0.20;
        private const double LogFloorAcm2 = 1e-20;
        private const double ExpClipMin = -50.0;
        private const double ExpClipMax = 50.0;
        private const int MinTafelPoints = 2;
        private const int MinHerPoints = 5;

        private readonly IReadOnlyList<ElectrochemicalReactionFactory> _reactionFactories;

        public BvCurveFitter()
            : this(new ElectrochemicalReactionFactory[]
            {
                new MetalOxidationFactory(),
                new ORRFactory(),
                new HERFactory()
            })
        {
        }

        internal BvCurveFitter(IEnumerable<ElectrochemicalReactionFactory> reactionFactories)
        {
            if (reactionFactories == null)
                throw new ArgumentNullException(nameof(reactionFactories));

            _reactionFactories = reactionFactories.ToArray();
            if (_reactionFactories.Count == 0)
                throw new ArgumentException("At least one reaction factory must be provided.", nameof(reactionFactories));
        }

        public BvModelParameters Fit(
            IReadOnlyList<double> potentialV,
            IReadOnlyList<double> currentDensityAcm2,
            double ecorrHintV,
            double temperatureCelsius,
            double electrolytePh,
            double metalIonConcentrationM,
            BvUserOverrides overrides = null)
        {
            if (potentialV.Count != currentDensityAcm2.Count)
                throw new ArgumentException("potentialV and currentDensityAcm2 must have the same length.");
            if (potentialV.Count == 0)
                throw new ArgumentException("Input arrays must not be empty.");

            bool includeMetal = overrides?.IncludeMetal ?? true;
            bool includeOrr   = overrides?.IncludeOrr   ?? true;
            bool includeHer   = overrides?.IncludeHer   ?? true;

            if (!includeMetal && !includeOrr && !includeHer)
                throw new ArgumentException("Select at least one reaction before fitting the polarization curve.", nameof(overrides));

            double[] e = [.. potentialV];
            double[] i = [.. currentDensityAcm2];

            double effectivePh = double.IsFinite(electrolytePh) ? electrolytePh : DefaultPh;
            double effectiveMetalIonConcentrationM =
                (double.IsFinite(metalIonConcentrationM) && metalIonConcentrationM > 0.0)
                    ? metalIonConcentrationM
                    : DefaultMetalIonConcentrationM;

            IReadOnlyList<IBvReaction> reactions = CreateReactionList(temperatureCelsius, effectivePh, effectiveMetalIonConcentrationM);
            double ecorr0 = EstimateEcorr(e, i, ecorrHintV);

            FitState initialState = EstimateInitialState(e, i, ecorr0, reactions, includeMetal, includeOrr, includeHer);
            ApplyOverrides(initialState, overrides);

            List<FitParameterBinding> bindings = BuildBindings(initialState, overrides);

            FitState fittedState;
            if (bindings.Count > 0)
            {
                double[] p0 = [.. bindings.Select(b => Math.Clamp(b.InitialValue, b.LowerBound, b.UpperBound))];
                double[] lb = [.. bindings.Select(b => b.LowerBound)];
                double[] ub = [.. bindings.Select(b => b.UpperBound)];

                double[] pFitted = LevenbergMarquardtSolver.Solve(
                    residualFunc: p => ComputeWeightedResiduals(e, i, p, initialState, bindings),
                    p0, lb, ub);

                fittedState = initialState.Clone();
                ApplyParameters(fittedState, bindings, pFitted);
            }
            else
            {
                fittedState = initialState.Clone();
            }

            BvModelParameters partialModel = ParametersToModel(fittedState);
            double ecorrFitted = FindEcorr(e, partialModel, ecorr0);
            return ParametersToModel(fittedState, ecorrFitted);
        }

        // ── Reaction list (replaces the old fixed ReactionSet) ────────────────────────────────────

        /// <summary>
        /// Creates one reaction per registered factory, sorted ascending by equilibrium potential.
        /// The order determines the per-reaction estimation sequence in
        /// <see cref="EstimateInitialState"/>.
        /// </summary>
        private IReadOnlyList<IBvReaction> CreateReactionList(
            double temperatureCelsius,
            double electrolytePh,
            double metalIonConcentrationM)
        {
            var reactions = new List<IBvReaction>(_reactionFactories.Count);
            var electrolyte = new ElectrolyteConditions(electrolytePh, temperatureCelsius, metalIonConcentrationM);
            foreach (ElectrochemicalReactionFactory factory in _reactionFactories)
            {
                reactions.Add(factory.CreateReaction(electrolyte));
            }

            reactions.Sort((left, right) => left.EquilibriumPotentialVshe.CompareTo(right.EquilibriumPotentialVshe));
            return reactions;
        }

        // ── Initial-state estimation ──────────────────────────────────────────────────────────────

        private static FitState EstimateInitialState(
            double[] e,
            double[] i,
            double ecorr,
            IReadOnlyList<IBvReaction> reactions,
            bool includeMetal,
            bool includeOrr,
            bool includeHer)
        {
            // Build per-reaction state objects in the same sorted order as the reaction list.
            var rfsList = new List<ReactionFitState>();
            foreach (IBvReaction reaction in reactions)
            {
                bool isIncluded = reaction.Name switch
                {
                    ReactionType.MetalOxidation    => includeMetal,
                    ReactionType.OxygenReduction   => includeOrr,
                    ReactionType.HydrogenEvolution => includeHer,
                    _                              => true
                };

                rfsList.Add(new ReactionFitState
                {
                    Reaction   = reaction,
                    IsIncluded = isIncluded,
                    I0         = reaction.I0MinAcm2,
                    Beta       = DefaultBeta,
                    Ilim       = reaction.IlimMinAcm2
                });
            }

            var state = new FitState { Reactions = rfsList };

            // Estimate ORR iLim first; it is subtracted as background when fitting HER.
            ReactionFitState orrRfs = state.TryGetReaction(ReactionType.OxygenReduction);
            if (orrRfs != null && orrRfs.IsIncluded)
                orrRfs.Ilim = EstimateIlimOrr(e, i, ecorr, orrRfs.Reaction);

            // Estimate kinetic parameters for each included reaction in sorted (low→high E_eq) order.
            foreach (ReactionFitState rfs in rfsList)
            {
                if (!rfs.IsIncluded)
                    continue;

                switch (rfs.Reaction.Name)
                {
                    case ReactionType.MetalOxidation:
                        FitMetalOxidation(e, i, ecorr, rfs.Reaction, out double betaMetal, out double i0Metal);
                        rfs.Beta = betaMetal;
                        rfs.I0   = i0Metal;
                        if (rfs.Reaction.IlimMaxAcm2 > 0.0)
                            rfs.Ilim = EstimateIlimMetal(e, i, ecorr, rfs.Reaction);
                        break;

                    case ReactionType.HydrogenEvolution:
                        double cathodicBackground = orrRfs?.IsIncluded == true ? orrRfs.Ilim : 0.0;
                        FitHer(e, i, cathodicBackground, rfs.Reaction, out double i0Her, out double betaHer);
                        rfs.I0   = i0Her;
                        rfs.Beta = betaHer;
                        // HER is limited only by water/proton diffusion — very large in practice.
                        if (rfs.Reaction.IlimMaxAcm2 > 0.0)
                            rfs.Ilim = rfs.Reaction.IlimMaxAcm2 * 0.5;
                        break;

                    case ReactionType.OxygenReduction:
                        FitOrrBv(e, i, ecorr, rfs.Reaction, out double betaOrr, out double i0Orr);
                        rfs.Beta = betaOrr;
                        rfs.I0   = i0Orr;
                        // Ilim already estimated above before the main loop.
                        break;
                }
            }

            return state;
        }

        // ── Override application ──────────────────────────────────────────────────────────────────

        private static void ApplyOverrides(FitState state, BvUserOverrides overrides)
        {
            if (overrides == null)
                return;

            ReactionFitState metalRfs = state.TryGetReaction(ReactionType.MetalOxidation);
            if (metalRfs != null)
            {
                if (overrides.I0Metal.HasValue)
                    metalRfs.I0   = Math.Clamp(overrides.I0Metal.Value,   metalRfs.Reaction.I0MinAcm2,   metalRfs.Reaction.I0MaxAcm2);
                if (overrides.BetaMetal.HasValue)
                    metalRfs.Beta = Math.Clamp(overrides.BetaMetal.Value,  metalRfs.Reaction.BetaMin,     metalRfs.Reaction.BetaMax);
                if (overrides.IlimMetal.HasValue && metalRfs.Reaction.IlimMaxAcm2 > 0.0)
                    metalRfs.Ilim = Math.Clamp(overrides.IlimMetal.Value,  metalRfs.Reaction.IlimMinAcm2, metalRfs.Reaction.IlimMaxAcm2);
            }

            ReactionFitState orrRfs = state.TryGetReaction(ReactionType.OxygenReduction);
            if (orrRfs != null)
            {
                if (overrides.I0Orr.HasValue)
                    orrRfs.I0   = Math.Clamp(overrides.I0Orr.Value,   orrRfs.Reaction.I0MinAcm2,   orrRfs.Reaction.I0MaxAcm2);
                if (overrides.BetaOrr.HasValue)
                    orrRfs.Beta = Math.Clamp(overrides.BetaOrr.Value,  orrRfs.Reaction.BetaMin,     orrRfs.Reaction.BetaMax);
                if (overrides.IlimOrr.HasValue)
                    orrRfs.Ilim = Math.Clamp(overrides.IlimOrr.Value,  orrRfs.Reaction.IlimMinAcm2, orrRfs.Reaction.IlimMaxAcm2);
            }

            ReactionFitState herRfs = state.TryGetReaction(ReactionType.HydrogenEvolution);
            if (herRfs != null)
            {
                if (overrides.I0Her.HasValue)
                    herRfs.I0   = Math.Clamp(overrides.I0Her.Value,   herRfs.Reaction.I0MinAcm2,   herRfs.Reaction.I0MaxAcm2);
                if (overrides.BetaHer.HasValue)
                    herRfs.Beta = Math.Clamp(overrides.BetaHer.Value,  herRfs.Reaction.BetaMin,     herRfs.Reaction.BetaMax);
                if (overrides.IlimHer.HasValue && herRfs.Reaction.IlimMaxAcm2 > 0.0)
                    herRfs.Ilim = Math.Clamp(overrides.IlimHer.Value,  herRfs.Reaction.IlimMinAcm2, herRfs.Reaction.IlimMaxAcm2);
            }
        }

        // ── LM parameter binding ──────────────────────────────────────────────────────────────────

        private static List<FitParameterBinding> BuildBindings(FitState state, BvUserOverrides overrides)
        {
            List<FitParameterBinding> bindings = [];

            foreach (ReactionFitState rfs in state.Reactions)
            {
                if (!rfs.IsIncluded)
                    continue;

                bool isFixed = rfs.Reaction.Name switch
                {
                    ReactionType.MetalOxidation    => overrides?.FixMetal ?? false,
                    ReactionType.OxygenReduction   => overrides?.FixOrr   ?? false,
                    ReactionType.HydrogenEvolution => overrides?.FixHer   ?? false,
                    _                              => false
                };

                if (isFixed)
                    continue;

                ReactionType name = rfs.Reaction.Name; // captured for closure
                AddParameter(bindings, rfs.I0,   rfs.Reaction.I0MinAcm2,   rfs.Reaction.I0MaxAcm2,
                    (s, val) => s.GetReaction(name).I0   = val);
                AddParameter(bindings, rfs.Beta,  rfs.Reaction.BetaMin,     rfs.Reaction.BetaMax,
                    (s, val) => s.GetReaction(name).Beta = val);

                if (rfs.Reaction.IlimMaxAcm2 > 0.0)
                    AddParameter(bindings, rfs.Ilim, rfs.Reaction.IlimMinAcm2, rfs.Reaction.IlimMaxAcm2,
                        (s, val) => s.GetReaction(name).Ilim = val);
            }

            return bindings;
        }

        private static void AddParameter(
            ICollection<FitParameterBinding> bindings,
            double initialValue,
            double lowerBound,
            double upperBound,
            Action<FitState, double> apply)
        {
            bindings.Add(new FitParameterBinding(initialValue, lowerBound, upperBound, apply));
        }

        private static void ApplyParameters(FitState state, IReadOnlyList<FitParameterBinding> bindings, double[] values)
        {
            for (int index = 0; index < bindings.Count; index++)
                bindings[index].Apply(state, values[index]);
        }

        // ── Ecorr estimation ──────────────────────────────────────────────────────────────────────

        private static double EstimateEcorr(double[] e, double[] i, double hint)
        {
            for (int k = 0; k < i.Length - 1; k++)
            {
                if (i[k] * i[k + 1] < 0.0)
                {
                    double e1 = e[k], e2 = e[k + 1];
                    double i1 = i[k], i2 = i[k + 1];
                    double dI = i2 - i1;
                    return dI != 0.0 ? e1 - i1 * (e2 - e1) / dI : (e1 + e2) / 2.0;
                }
            }

            if (double.IsFinite(hint))
                return hint;

            int minIdx = 0;
            double minAbsI = Math.Abs(i[0]);
            for (int k = 1; k < i.Length; k++)
            {
                double absI = Math.Abs(i[k]);
                if (absI < minAbsI)
                {
                    minAbsI = absI;
                    minIdx = k;
                }
            }

            return e[minIdx];
        }

        // ── Per-reaction parameter estimators ─────────────────────────────────────────────────────

        private static void FitMetalOxidation(
            double[] e,
            double[] i,
            double ecorr,
            IBvReaction reaction,
            out double betaMetal,
            out double i0Metal)
        {
            betaMetal = DefaultBeta;
            i0Metal   = Math.Clamp(DefaultI0MetalAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);

            double eMin = ecorr + TafelLowerOffsetV;
            double eMax = ecorr + TafelUpperOffsetV;
            double[] eWin = [.. e.Where((_, k) => e[k] >= eMin && e[k] <= eMax)];
            double[] iWin = [.. i.Where((_, k) => e[k] >= eMin && e[k] <= eMax)];

            if (eWin.Length < MinTafelPoints)
                return;

            double[] logI = [.. iWin.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2)))];
            if (!OlsFit(eWin, logI, out double slope, out double intercept))
                return;

            if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-30)
                return;

            double zFoverRTln10 = reaction.Z * ElectrochemicalConstants.F
                                  / (ElectrochemicalConstants.R * reaction.TemperatureKelvin * Math.Log(10.0));
            double betaFit     = slope / zFoverRTln10;
            double i0MetalFit  = Math.Pow(10.0, slope * reaction.EquilibriumPotentialVshe + intercept);

            if (double.IsFinite(betaFit) && double.IsFinite(i0MetalFit))
            {
                betaMetal = Math.Clamp(betaFit,    reaction.BetaMin,   reaction.BetaMax);
                i0Metal   = Math.Clamp(i0MetalFit, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }
        }

        private static double EstimateIlimMetal(double[] e, double[] i, double ecorr, IBvReaction reaction)
        {
            // The cathodic limiting current for metal deposition is set by the very low dissolved
            // cation concentration. Estimate from the minimum |i| in the deeply cathodic potential
            // region, then clamp to [IlimMinAcm2, IlimMaxAcm2] = [1e-14, 1e-6] A/cm2.
            if (e.Length == 0)
                return Math.Clamp(reaction.IlimMaxAcm2 * 0.1, reaction.IlimMinAcm2, reaction.IlimMaxAcm2);

            double eMin    = e.Min();
            double eRange  = e.Max() - eMin;
            double cutoff  = eMin + eRange * IlimDepthFraction;

            double[] deepAbsI = [.. i
                .Where((_, k) => e[k] <= cutoff && e[k] < ecorr)
                .Select(v => Math.Abs(v))];

            double raw = deepAbsI.Length > 0
                ? deepAbsI.Min()
                : reaction.IlimMaxAcm2 * 0.1;

            return Math.Clamp(raw, reaction.IlimMinAcm2, reaction.IlimMaxAcm2);
        }

        private static void FitOrrBv(
            double[] e,
            double[] i,
            double ecorr,
            IBvReaction reaction,
            out double betaOrr,
            out double i0Orr)
        {
            betaOrr = DefaultBeta;
            i0Orr   = Math.Clamp(DefaultI0OrrAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);

            double eMin = ecorr - TafelUpperOffsetV;
            double eMax = ecorr - TafelLowerOffsetV;
            double[] eWin = [.. e.Where((_, k) => e[k] >= eMin && e[k] <= eMax)];
            double[] iWin = [.. i.Where((_, k) => e[k] >= eMin && e[k] <= eMax)];

            if (eWin.Length < MinTafelPoints)
                return;

            double[] logI = [.. iWin.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2)))];
            if (!OlsFit(eWin, logI, out double slope, out double intercept))
                return;

            if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-30)
                return;

            double zFoverRTln10 = reaction.Z * ElectrochemicalConstants.F
                                  / (ElectrochemicalConstants.R * reaction.TemperatureKelvin * Math.Log(10.0));
            double oneMinusBeta = -slope / zFoverRTln10;
            double betaFit      = 1.0 - oneMinusBeta;
            double i0OrrFit     = Math.Pow(10.0, slope * reaction.EquilibriumPotentialVshe + intercept);

            if (double.IsFinite(betaFit) && double.IsFinite(i0OrrFit))
            {
                betaOrr = Math.Clamp(betaFit,   reaction.BetaMin,   reaction.BetaMax);
                i0Orr   = Math.Clamp(i0OrrFit,  reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }
        }

        private static double EstimateIlimOrr(double[] e, double[] i, double ecorr, IBvReaction reaction)
        {
            if (e.Length == 0)
                return Math.Clamp(DefaultIlimOrrAcm2, reaction.IlimMinAcm2, reaction.IlimMaxAcm2);

            double eMin   = e.Min();
            double eRange = e.Max() - eMin;
            double cutoff = eMin + eRange * IlimDepthFraction;

            double[] deepI = [.. i
                .Where((_, k) => e[k] <= cutoff && e[k] < ecorr)
                .Select(v => Math.Abs(v))];

            if (deepI.Length == 0)
            {
                double[] cathodicI = [.. i.Where((_, k) => e[k] < ecorr).Select(v => Math.Abs(v))];
                if (cathodicI.Length == 0)
                    return Math.Clamp(DefaultIlimOrrAcm2, reaction.IlimMinAcm2, reaction.IlimMaxAcm2);

                return Math.Clamp(Percentile(cathodicI, 75), reaction.IlimMinAcm2, reaction.IlimMaxAcm2);
            }

            return Math.Clamp(Median(deepI), reaction.IlimMinAcm2, reaction.IlimMaxAcm2);
        }

        private static void FitHer(
            double[] e,
            double[] i,
            double cathodicBackground,
            IBvReaction reaction,
            out double i0Her,
            out double betaHer)
        {
            i0Her   = Math.Clamp(DefaultI0HerAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            betaHer = DefaultBeta;

            double eEq    = reaction.EquilibriumPotentialVshe;
            double eWinHi = eEq - 0.02;
            double eWinLo = eEq - 0.40;

            List<double> eWin = [];
            List<double> iWin = [];
            for (int k = 0; k < e.Length; k++)
            {
                if (e[k] >= eWinLo && e[k] <= eWinHi)
                {
                    double iResidual = Math.Abs(i[k]) - cathodicBackground;
                    if (iResidual > LogFloorAcm2)
                    {
                        eWin.Add(e[k]);
                        iWin.Add(-iResidual);
                    }
                }
            }

            if (eWin.Count < MinHerPoints)
                return;

            double[] eArr  = [.. eWin];
            double[] iArr  = [.. iWin];
            double[] eta   = [.. eArr.Select(v => v - eEq)];
            double[] logI  = [.. iArr.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2)))];
            double zFoverRTln10 = reaction.Z * ElectrochemicalConstants.F
                                  / (ElectrochemicalConstants.R * reaction.TemperatureKelvin * Math.Log(10.0));

            if (OlsFit(eta, logI, out double slope, out double intercept))
            {
                double betaSeed = 1.0 + slope / zFoverRTln10;
                double i0Seed   = Math.Pow(10.0, intercept);
                betaHer = Math.Clamp(betaSeed, reaction.BetaMin,   reaction.BetaMax);
                i0Her   = Math.Clamp(i0Seed,   reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }

            double zFoverRT   = reaction.Z * ElectrochemicalConstants.F
                                / (ElectrochemicalConstants.R * reaction.TemperatureKelvin);
            double[] p0Her    = { i0Her, betaHer };
            double[] lbHer    = { reaction.I0MinAcm2, reaction.BetaMin };
            double[] ubHer    = { reaction.I0MaxAcm2, reaction.BetaMax };
            double[] weightHer = [.. iArr.Select(v => 1.0 / Math.Max(Math.Abs(v), 1e-12))];

            double[] pHerFitted = LevenbergMarquardtSolver.Solve(
                p =>
                {
                    var residuals = new double[eArr.Length];
                    for (int k = 0; k < eArr.Length; k++)
                    {
                        double etaK  = eArr[k] - eEq;
                        double iModel = -p[0] * Math.Exp(
                            Math.Clamp(-(1.0 - p[1]) * zFoverRT * etaK, ExpClipMin, ExpClipMax));
                        residuals[k] = (iModel - iArr[k]) * weightHer[k];
                    }
                    return residuals;
                },
                p0Her, lbHer, ubHer);

            i0Her   = Math.Clamp(pHerFitted[0], reaction.I0MinAcm2, reaction.I0MaxAcm2);
            betaHer = Math.Clamp(pHerFitted[1], reaction.BetaMin,   reaction.BetaMax);
        }

        // ── Ecorr post-fit refinement ─────────────────────────────────────────────────────────────

        private static double FindEcorr(double[] e, BvModelParameters model, double ecorrHint)
        {
            if (e.Length == 0)
                return ecorrHint;

            double eMin = e.Min();
            double eMax = e.Max();
            const int scanSteps = 500;
            double step  = (eMax - eMin) / scanSteps;
            double ePrev = eMin;
            double iPrev = model.ComputeCurrentDensity(eMin);

            for (int k = 1; k <= scanSteps; k++)
            {
                double eCurr = eMin + k * step;
                double iCurr = model.ComputeCurrentDensity(eCurr);
                if (iPrev * iCurr < 0.0)
                {
                    double dI = iCurr - iPrev;
                    return dI != 0.0 ? ePrev - iPrev * (eCurr - ePrev) / dI : (ePrev + eCurr) / 2.0;
                }

                ePrev = eCurr;
                iPrev = iCurr;
            }

            return ecorrHint;
        }

        // ── Residual computation (log-space for equal per-decade weighting) ───────────────────────

        private static double[] ComputeWeightedResiduals(
            double[] e,
            double[] iMeasured,
            double[] p,
            FitState baseState,
            IReadOnlyList<FitParameterBinding> bindings)
        {
            FitState state = baseState.Clone();
            ApplyParameters(state, bindings, p);
            BvModelParameters model = ParametersToModel(state);

            // Residuals in log10(|i|) space give equal weight per decade of current,
            // preventing the optimizer from over-fitting the low-current cathodic region
            // at the expense of the anodic branch.
            double[] residuals = new double[e.Length];
            for (int k = 0; k < e.Length; k++)
            {
                double absModel = Math.Max(Math.Abs(model.ComputeCurrentDensity(e[k])), LogFloorAcm2);
                double absMeas  = Math.Max(Math.Abs(iMeasured[k]),                       LogFloorAcm2);
                residuals[k] = Math.Log10(absModel) - Math.Log10(absMeas);
            }

            return residuals;
        }

        // ── Model assembly ────────────────────────────────────────────────────────────────────────

        private static BvModelParameters ParametersToModel(FitState state, double ecorr = 0.0)
        {
            var reactionParams = state.Reactions.Select(rfs =>
                new BvModelParameters.ReactionParameters(
                    rfs.Reaction,
                    rfs.I0,
                    rfs.Beta,
                    rfs.Reaction.IlimMaxAcm2 > 0.0 ? rfs.Ilim : 0.0,
                    rfs.IsIncluded)).ToList();

            return new BvModelParameters(reactionParams) { Ecorr = ecorr };
        }

        // ── Numerical helpers ─────────────────────────────────────────────────────────────────────

        private static bool OlsFit(double[] x, double[] y, out double slope, out double intercept)
        {
            slope     = 0.0;
            intercept = 0.0;

            int n = x.Length;
            if (n < 2)
                return false;

            double sumX = 0.0, sumY = 0.0, sumXY = 0.0, sumX2 = 0.0;
            foreach (double v in x) sumX  += v;
            foreach (double v in y) sumY  += v;
            for (int k = 0; k < n; k++)
            {
                sumXY += x[k] * y[k];
                sumX2 += x[k] * x[k];
            }

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-30)
                return false;

            slope     = (n * sumXY - sumX * sumY) / denom;
            intercept = (sumY - slope * sumX) / n;
            return true;
        }

        private static double Median(double[] values)
        {
            if (values.Length == 0)
                throw new InvalidOperationException("Cannot compute median of an empty array.");

            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int mid = sorted.Length / 2;
            return sorted.Length % 2 == 1
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2.0;
        }

        private static double Percentile(double[] values, double percentile)
        {
            if (values.Length == 0)
                return 0.0;

            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);

            double idx  = (percentile / 100.0) * (sorted.Length - 1);
            int    lo   = (int)Math.Floor(idx);
            int    hi   = Math.Min(lo + 1, sorted.Length - 1);
            double frac = idx - lo;
            return sorted[lo] * (1.0 - frac) + sorted[hi] * frac;
        }

        // ── Private inner types ───────────────────────────────────────────────────────────────────

        private sealed class ReactionFitState
        {
            public IBvReaction Reaction   { get; init; }
            public bool        IsIncluded { get; set; }
            public double      I0         { get; set; }
            public double      Beta       { get; set; }
            public double      Ilim       { get; set; }

            public ReactionFitState Clone() => new ReactionFitState
            {
                Reaction   = Reaction,
                IsIncluded = IsIncluded,
                I0         = I0,
                Beta       = Beta,
                Ilim       = Ilim
            };
        }

        private sealed class FitState
        {
            public List<ReactionFitState> Reactions { get; init; }

            /// <summary>
            /// Returns the ReactionFitState for the given reaction type.
            /// Throws <see cref="InvalidOperationException"/> if not found; only call this
            /// from closures that were created while the reaction is known to be in the list.
            /// </summary>
            public ReactionFitState GetReaction(ReactionType name) =>
                Reactions.First(r => r.Reaction.Name == name);

            public ReactionFitState TryGetReaction(ReactionType name) =>
                Reactions.FirstOrDefault(r => r.Reaction.Name == name);

            public FitState Clone() => new FitState
            {
                Reactions = Reactions.Select(r => r.Clone()).ToList()
            };
        }

        private sealed class FitParameterBinding
        {
            public FitParameterBinding(
                double initialValue,
                double lowerBound,
                double upperBound,
                Action<FitState, double> apply)
            {
                InitialValue = initialValue;
                LowerBound   = lowerBound;
                UpperBound   = upperBound;
                Apply        = apply;
            }

            public double InitialValue { get; }
            public double LowerBound   { get; }
            public double UpperBound   { get; }
            public Action<FitState, double> Apply { get; }
        }
    }
}
