using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;

using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Fits a Butler-Volmer model to a merged polarization curve using a dynamic parameter vector
    /// that only includes the reactions selected for the analysis run.
    /// </summary>
    public sealed class BvCurveFitter : IBvCurveFitter
    {
        private const double DefaultPh = 8.0;
        private const double TafelLowerOffsetV = 0.01;
        private const double TafelUpperOffsetV = 0.15;
        private const double DefaultBeta = 0.5;
        private const double DefaultI0MetalAcm2 = 1e-8;
        private const double DefaultI0OrrAcm2 = 1e-8;
        private const double DefaultI0HerAcm2 = 1e-9;
        private const double DefaultIlimOrrAcm2 = 1e-6;
        private const double IlimOrrDepthFraction = 0.20;
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
            BvUserOverrides overrides = null)
        {
            if (potentialV.Count != currentDensityAcm2.Count)
                throw new ArgumentException("potentialV and currentDensityAcm2 must have the same length.");
            if (potentialV.Count == 0)
                throw new ArgumentException("Input arrays must not be empty.");

            bool includeMetal = overrides?.IncludeMetal ?? true;
            bool includeOrr = overrides?.IncludeOrr ?? true;
            bool includeHer = overrides?.IncludeHer ?? true;

            if (!includeMetal && !includeOrr && !includeHer)
                throw new ArgumentException("Select at least one reaction before fitting the polarization curve.", nameof(overrides));

            double[] e = [.. potentialV];
            double[] i = [.. currentDensityAcm2];

            ReactionSet reactions = CreateReactionSet(temperatureCelsius);
            double ecorr0 = EstimateEcorr(e, i, ecorrHintV);

            FitState initialState = EstimateInitialState(e, i, ecorr0, reactions, includeMetal, includeOrr, includeHer);
            ApplyOverrides(initialState, overrides, reactions);

            List<FitParameterBinding> bindings = BuildBindings(initialState, overrides, reactions);

            double[] absI = [.. i.Select(selector: v => Math.Abs(v))];
            double p20 = Percentile(absI, 20);
            double[] weight = [.. absI.Select(selector: v => 1.0 / Math.Max(v, p20))];

            FitState fittedState;
            if (bindings.Count > 0)
            {
                double[] p0 = [.. bindings.Select(b => Math.Clamp(b.InitialValue, b.LowerBound, b.UpperBound))];
                double[] lb = [.. bindings.Select(b => b.LowerBound)];
                double[] ub = [.. bindings.Select(b => b.UpperBound)];

                double[] pFitted = LevenbergMarquardtSolver.Solve(
                    residualFunc: p => ComputeWeightedResiduals(e, i, weight, p, initialState, bindings, reactions),
                    p0, lb, ub);

                fittedState = ApplyParameters(initialState.Clone(), bindings, pFitted);
            }
            else
            {
                fittedState = initialState.Clone();
            }

            BvModelParameters partialModel = ParametersToModel(fittedState, reactions);
            double ecorrFitted = FindEcorr(e, partialModel, ecorr0);
            return ParametersToModel(fittedState, reactions, ecorrFitted);
        }

        private ReactionSet CreateReactionSet(double temperatureCelsius) =>
            new ReactionSet(
                CreateReaction(ReactionType.MetalOxidation, temperatureCelsius),
                CreateReaction(ReactionType.OxygenReduction, temperatureCelsius),
                CreateReaction(ReactionType.HydrogenEvolution, temperatureCelsius));

        private IBvReaction CreateReaction(ReactionType reactionType, double temperatureCelsius)
        {
            foreach (ElectrochemicalReactionFactory factory in _reactionFactories)
            {
                if (factory.CanCreateReaction(reactionType))
                    return factory.CreateReaction(DefaultPh, temperatureCelsius);
            }

            throw new InvalidOperationException($"No reaction factory is registered for {reactionType}.");
        }

        private static FitState EstimateInitialState(
            double[] e,
            double[] i,
            double ecorr,
            ReactionSet reactions,
            bool includeMetal,
            bool includeOrr,
            bool includeHer)
        {
            FitState state = new()
            {
                IncludeMetal = includeMetal,
                IncludeOrr = includeOrr,
                IncludeHer = includeHer,
                I0Metal = reactions.Metal.I0MinAcm2,
                BetaMetal = DefaultBeta,
                I0Orr = reactions.Orr.I0MinAcm2,
                BetaOrr = DefaultBeta,
                IlimOrr = reactions.Orr.IlimMinAcm2,
                I0Her = reactions.Her.I0MinAcm2,
                BetaHer = DefaultBeta
            };

            if (includeMetal)
            {
                FitMetalOxidation(e, i, ecorr, reactions.Metal, out double betaMetal, out double i0Metal);
                state.BetaMetal = betaMetal;
                state.I0Metal = i0Metal;
            }
            else
            {
                state.I0Metal = reactions.Metal.I0MinAcm2;
                state.BetaMetal = DefaultBeta;
            }

            if (includeOrr)
            {
                double ilim = EstimateIlimOrr(e, i, ecorr, reactions.Orr);
                FitOrrBv(e, i, ecorr, reactions.Orr, out double betaOrr, out double i0Orr);
                state.IlimOrr = ilim;
                state.BetaOrr = betaOrr;
                state.I0Orr = i0Orr;
            }
            else
            {
                state.I0Orr = reactions.Orr.I0MinAcm2;
                state.BetaOrr = DefaultBeta;
                state.IlimOrr = reactions.Orr.IlimMinAcm2;
            }

            if (includeHer)
            {
                double cathodicBackground = includeOrr ? state.IlimOrr : 0.0;
                FitHer(e, i, cathodicBackground, reactions.Her, out double i0Her, out double betaHer);
                state.I0Her = i0Her;
                state.BetaHer = betaHer;
            }
            else
            {
                state.I0Her = reactions.Her.I0MinAcm2;
                state.BetaHer = DefaultBeta;
            }

            return state;
        }

        private static void ApplyOverrides(FitState state, BvUserOverrides overrides, ReactionSet reactions)
        {
            if (overrides == null)
                return;

            if (overrides.I0Metal.HasValue)
                state.I0Metal = Math.Clamp(overrides.I0Metal.Value, reactions.Metal.I0MinAcm2, reactions.Metal.I0MaxAcm2);
            if (overrides.BetaMetal.HasValue)
                state.BetaMetal = Math.Clamp(overrides.BetaMetal.Value, reactions.Metal.BetaMin, reactions.Metal.BetaMax);

            if (overrides.I0Orr.HasValue)
                state.I0Orr = Math.Clamp(overrides.I0Orr.Value, reactions.Orr.I0MinAcm2, reactions.Orr.I0MaxAcm2);
            if (overrides.BetaOrr.HasValue)
                state.BetaOrr = Math.Clamp(overrides.BetaOrr.Value, reactions.Orr.BetaMin, reactions.Orr.BetaMax);
            if (overrides.IlimOrr.HasValue)
                state.IlimOrr = Math.Clamp(overrides.IlimOrr.Value, reactions.Orr.IlimMinAcm2, reactions.Orr.IlimMaxAcm2);

            if (overrides.I0Her.HasValue)
                state.I0Her = Math.Clamp(overrides.I0Her.Value, reactions.Her.I0MinAcm2, reactions.Her.I0MaxAcm2);
            if (overrides.BetaHer.HasValue)
                state.BetaHer = Math.Clamp(overrides.BetaHer.Value, reactions.Her.BetaMin, reactions.Her.BetaMax);
        }

        private static List<FitParameterBinding> BuildBindings(FitState state, BvUserOverrides overrides, ReactionSet reactions)
        {
            List<FitParameterBinding> bindings = [];

            if (state.IncludeMetal && !(overrides?.FixMetal ?? false))
            {
                AddParameter(bindings, state.I0Metal, reactions.Metal.I0MinAcm2, reactions.Metal.I0MaxAcm2, (s, value) => s.I0Metal = value);
                AddParameter(bindings, state.BetaMetal, reactions.Metal.BetaMin, reactions.Metal.BetaMax, (s, value) => s.BetaMetal = value);
            }

            if (state.IncludeOrr && !(overrides?.FixOrr ?? false))
            {
                AddParameter(bindings, state.I0Orr, reactions.Orr.I0MinAcm2, reactions.Orr.I0MaxAcm2, (s, value) => s.I0Orr = value);
                AddParameter(bindings, state.BetaOrr, reactions.Orr.BetaMin, reactions.Orr.BetaMax, (s, value) => s.BetaOrr = value);
                AddParameter(bindings, state.IlimOrr, reactions.Orr.IlimMinAcm2, reactions.Orr.IlimMaxAcm2, (s, value) => s.IlimOrr = value);
            }

            if (state.IncludeHer && !(overrides?.FixHer ?? false))
            {
                AddParameter(bindings, state.I0Her, reactions.Her.I0MinAcm2, reactions.Her.I0MaxAcm2, (s, value) => s.I0Her = value);
                AddParameter(bindings, state.BetaHer, reactions.Her.BetaMin, reactions.Her.BetaMax, (s, value) => s.BetaHer = value);
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

        private static FitState ApplyParameters(FitState state, IReadOnlyList<FitParameterBinding> bindings, double[] values)
        {
            for (int index = 0; index < bindings.Count; index++)
                bindings[index].Apply(state, values[index]);

            return state;
        }

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

        private static void FitMetalOxidation(
            double[] e,
            double[] i,
            double ecorr,
            IBvReaction reaction,
            out double betaMetal,
            out double i0Metal)
        {
            betaMetal = DefaultBeta;
            i0Metal = Math.Clamp(DefaultI0MetalAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);

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
            double betaFit = slope / zFoverRTln10;
            double i0MetalFit = Math.Pow(10.0, slope * reaction.EquilibriumPotentialVshe + intercept);

            if (double.IsFinite(betaFit) && double.IsFinite(i0MetalFit))
            {
                betaMetal = Math.Clamp(betaFit, reaction.BetaMin, reaction.BetaMax);
                i0Metal = Math.Clamp(i0MetalFit, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }
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
            i0Orr = Math.Clamp(DefaultI0OrrAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);

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
            double betaFit = 1.0 - oneMinusBeta;
            double i0OrrFit = Math.Pow(10.0, slope * reaction.EquilibriumPotentialVshe + intercept);

            if (double.IsFinite(betaFit) && double.IsFinite(i0OrrFit))
            {
                betaOrr = Math.Clamp(betaFit, reaction.BetaMin, reaction.BetaMax);
                i0Orr = Math.Clamp(i0OrrFit, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }
        }

        private static double EstimateIlimOrr(double[] e, double[] i, double ecorr, IBvReaction reaction)
        {
            if (e.Length == 0)
                return Math.Clamp(DefaultIlimOrrAcm2, reaction.IlimMinAcm2, reaction.IlimMaxAcm2);

            double eMin = e.Min();
            double eRange = e.Max() - eMin;
            double cutoff = eMin + eRange * IlimOrrDepthFraction;

            double[] deepI = [.. i
                .Where((_, k) => e[k] <= cutoff && e[k] < ecorr)
                .Select(selector: v => Math.Abs(v))];

            if (deepI.Length == 0)
            {
                double[] cathodicI = [.. i.Where((_, k) => e[k] < ecorr).Select(selector: v => Math.Abs(v))];
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
            i0Her = Math.Clamp(DefaultI0HerAcm2, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            betaHer = DefaultBeta;

            double eEq = reaction.EquilibriumPotentialVshe;
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

            double[] eArr = [.. eWin];
            double[] iArr = [.. iWin];
            double[] eta = [.. eArr.Select(selector: v => v - eEq)];
            double[] logI = [.. iArr.Select(selector: v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2)))];
            double zFoverRTln10 = reaction.Z * ElectrochemicalConstants.F
                                  / (ElectrochemicalConstants.R * reaction.TemperatureKelvin * Math.Log(10.0));

            if (OlsFit(eta, logI, out double slope, out double intercept))
            {
                double betaSeed = 1.0 + slope / zFoverRTln10;
                double i0Seed = Math.Pow(10.0, intercept);
                betaHer = Math.Clamp(betaSeed, reaction.BetaMin, reaction.BetaMax);
                i0Her = Math.Clamp(i0Seed, reaction.I0MinAcm2, reaction.I0MaxAcm2);
            }

            double zFoverRT = reaction.Z * ElectrochemicalConstants.F
                              / (ElectrochemicalConstants.R * reaction.TemperatureKelvin);
            double[] p0Her = { i0Her, betaHer };
            double[] lbHer = { reaction.I0MinAcm2, reaction.BetaMin };
            double[] ubHer = { reaction.I0MaxAcm2, reaction.BetaMax };
            double[] weightHer = [.. iArr.Select(v => 1.0 / Math.Max(Math.Abs(v), 1e-12))];

            double[] pHerFitted = LevenbergMarquardtSolver.Solve(
                p =>
                {
                    var residuals = new double[eArr.Length];
                    for (int k = 0; k < eArr.Length; k++)
                    {
                        double etaK = eArr[k] - eEq;
                        double iModel = -p[0] * Math.Exp(
                            Math.Clamp(-(1.0 - p[1]) * zFoverRT * etaK, ExpClipMin, ExpClipMax));
                        residuals[k] = (iModel - iArr[k]) * weightHer[k];
                    }
                    return residuals;
                },
                p0Her, lbHer, ubHer);

            i0Her = Math.Clamp(pHerFitted[0], reaction.I0MinAcm2, reaction.I0MaxAcm2);
            betaHer = Math.Clamp(pHerFitted[1], reaction.BetaMin, reaction.BetaMax);
        }

        private static double FindEcorr(double[] e, BvModelParameters model, double ecorrHint)
        {
            if (e.Length == 0)
                return ecorrHint;

            double eMin = e.Min();
            double eMax = e.Max();
            const int scanSteps = 500;
            double step = (eMax - eMin) / scanSteps;
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

        private static double[] ComputeWeightedResiduals(
            double[] e,
            double[] iMeasured,
            double[] weight,
            double[] p,
            FitState baseState,
            IReadOnlyList<FitParameterBinding> bindings,
            ReactionSet reactions)
        {
            FitState state = ApplyParameters(baseState.Clone(), bindings, p);
            BvModelParameters model = ParametersToModel(state, reactions);
            double[] residuals = new double[e.Length];
            for (int k = 0; k < e.Length; k++)
            {
                double iModel = model.ComputeCurrentDensity(e[k]);
                residuals[k] = (iModel - iMeasured[k]) * weight[k];
            }

            return residuals;
        }

        private static BvModelParameters ParametersToModel(FitState state, ReactionSet reactions, double ecorr = 0.0) =>
            new BvModelParameters(reactions.Metal, reactions.Orr, reactions.Her)
            {
                I0Metal = state.I0Metal,
                BetaMetal = state.BetaMetal,
                EMetalEquilibriumV = reactions.Metal.EquilibriumPotentialVshe,
                I0Orr = state.I0Orr,
                BetaOrr = state.BetaOrr,
                IlimOrr = state.IlimOrr,
                EorrEquilibriumV = reactions.Orr.EquilibriumPotentialVshe,
                I0Her = state.I0Her,
                BetaHer = state.BetaHer,
                EherEquilibriumV = reactions.Her.EquilibriumPotentialVshe,
                Ecorr = ecorr,
                IncludeMetal = state.IncludeMetal,
                IncludeOrr = state.IncludeOrr,
                IncludeHer = state.IncludeHer,
            };

        private static bool OlsFit(double[] x, double[] y, out double slope, out double intercept)
        {
            slope = 0.0;
            intercept = 0.0;

            int n = x.Length;
            if (n < 2)
                return false;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXY = 0.0;
            double sumX2 = 0.0;
            foreach (double v in x)
                sumX += v;
            foreach (double v in y)
                sumY += v;
            for (int k = 0; k < n; k++)
            {
                sumXY += x[k] * y[k];
                sumX2 += x[k] * x[k];
            }

            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-30)
                return false;

            slope = (n * sumXY - sumX * sumY) / denom;
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

            double idx = (percentile / 100.0) * (sorted.Length - 1);
            int lo = (int)Math.Floor(idx);
            int hi = Math.Min(lo + 1, sorted.Length - 1);
            double frac = idx - lo;
            return sorted[lo] * (1.0 - frac) + sorted[hi] * frac;
        }

        private sealed class ReactionSet
        {
            public ReactionSet(IBvReaction metal, IBvReaction orr, IBvReaction her)
            {
                Metal = metal;
                Orr = orr;
                Her = her;
            }

            public IBvReaction Metal { get; }
            public IBvReaction Orr { get; }
            public IBvReaction Her { get; }
        }

        private sealed class FitState
        {
            public bool IncludeMetal { get; init; }
            public bool IncludeOrr { get; init; }
            public bool IncludeHer { get; init; }
            public double I0Metal { get; set; }
            public double BetaMetal { get; set; }
            public double I0Orr { get; set; }
            public double BetaOrr { get; set; }
            public double IlimOrr { get; set; }
            public double I0Her { get; set; }
            public double BetaHer { get; set; }

            public FitState Clone() => new FitState
            {
                IncludeMetal = IncludeMetal,
                IncludeOrr = IncludeOrr,
                IncludeHer = IncludeHer,
                I0Metal = I0Metal,
                BetaMetal = BetaMetal,
                I0Orr = I0Orr,
                BetaOrr = BetaOrr,
                IlimOrr = IlimOrr,
                I0Her = I0Her,
                BetaHer = BetaHer,
            };
        }

        private sealed class FitParameterBinding
        {
            public FitParameterBinding(double initialValue, double lowerBound, double upperBound, Action<FitState, double> apply)
            {
                InitialValue = initialValue;
                LowerBound = lowerBound;
                UpperBound = upperBound;
                Apply = apply;
            }

            public double InitialValue { get; }
            public double LowerBound { get; }
            public double UpperBound { get; }
            public Action<FitState, double> Apply { get; }
        }
    }
}
