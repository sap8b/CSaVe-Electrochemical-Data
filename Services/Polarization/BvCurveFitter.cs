using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Fits the seven-parameter Butler-Volmer model to a merged polarization curve using a
/// multi-step initialisation followed by a Levenberg-Marquardt global polish.
/// All three half-reactions — metal oxidation, ORR, and HER — use the full Butler-Volmer
/// equation with Nernst-fixed equilibrium potentials supplied by <see cref="ElectrochemicalReaction"/>
/// objects.  The ORR also includes a Koutecky-Levich mass-transport correction.
/// All arithmetic uses <see cref="System.Math"/> only; no external libraries are required.
/// </summary>
public sealed class BvCurveFitter : IBvCurveFitter
{
    // ── Module-level reaction singletons ──────────────────────────────────────────────────────
    /// <summary>Fixed thermodynamic constants for the metal oxidation half-reaction (Fe/Fe²⁺, E0 = −0.44 V vs. SHE).</summary>
    private static readonly ElectrochemicalReaction MetalReaction =
        new ElectrochemicalReaction(name: "Metal", e0Vshe: -0.54, z: 2, pH: 8.0, temperatureCelsius: 25.0); // This is technically the wrong potential for Fe/Fe2+, but this is what the polarization curve is giving us, so maybe the reference electrode was off?

    /// <summary>Fixed thermodynamic constants for the ORR half-reaction (O₂/H₂O, E0 = 1.229 V vs. SHE at pH 0).</summary>
    private static readonly ElectrochemicalReaction OrrReaction =
        new ElectrochemicalReaction(name: "ORR", e0Vshe: 1.229, z: 4, pH: 8.0, temperatureCelsius: 25.0);

    /// <summary>Fixed thermodynamic constants for the HER half-reaction (E0 = 0 V vs. SHE at pH 0).</summary>
    private static readonly ElectrochemicalReaction HerReaction =
        new ElectrochemicalReaction(name: "HER", e0Vshe: 0.0, z: 2, pH: 8.0, temperatureCelsius: 25.0);

    // ── Tafel window offsets relative to Ecorr ────────────────────────────────────────────────
    // Lower offset: skip the near-linear mixed-potential zone where neither branch is dominant.
    private const double TafelLowerOffsetV  = 0.01;   // 0.01 V below/above Ecorr

    // Upper offset: stay within the true Tafel region before diffusion effects set in.
    private const double TafelUpperOffsetV  = 0.15;   // 0.15 V below/above Ecorr

    // ── Default fall-back values when regression windows have too few points ──────────────────
    // Default metal symmetry factor — symmetric BV as default.
    private const double DefaultBetaMetal   = 0.5;

    // Default metal exchange current density (A/cm²) — representative of mild steel at OCP.
    private const double DefaultI0MetalAcm2 = 1e-8;

    // Default ORR symmetry factor — symmetric BV as default.
    private const double DefaultBetaOrr     = 0.5;

    // Default ORR exchange current density (A/cm²).
    private const double DefaultI0OrrAcm2   = 1e-8;

    // Default HER exchange current density (A/cm²).
    private const double DefaultI0HerAcm2   = 1e-9;

    // ── Fitted-parameter box bounds ───────────────────────────────────────────────────────────
    // Minimum physically meaningful exchange current density (A/cm²).
    private const double I0MinAcm2          = 1.0e-30;

    // Maximum exchange current density (A/cm²) before the solution is non-physical.
    private const double I0MaxAcm2          = 1e-1;

    // Minimum symmetry factor (dimensionless, 0 < β < 1).
    private const double BetaSymMin         = 0.01;

    // Maximum symmetry factor (dimensionless, 0 < β < 1).
    private const double BetaSymMax         = 0.99;

    // Maximum ORR limiting current density (A/cm²).
    private const double IlimOrrMaxAcm2     = 1.0;

    // Minimum ORR limiting current density (A/cm²).
    private const double IlimOrrMinAcm2     = 1e-10;

    // Lowest cathodic potential fraction for ilim_orr estimation (bottom 20 % of range).
    // The ORR plateau is most clearly visible in the deepest cathodic region.
    private const double IlimOrrDepthFraction = 0.20;

    // Floor applied before log10 to prevent log(0) errors.
    private const double LogFloorAcm2       = 1e-20;

    // Exponential argument clip limits — prevents overflow in exp() while retaining all physically meaningful values.
    private const double ExpClipMin         = -50.0;
    private const double ExpClipMax         =  50.0;

    // Minimum number of Tafel-region points required before running OLS regression.
    private const int MinTafelPoints        = 2;

    // Minimum number of HER-region points required before running HER regression.
    private const int MinHerPoints          = 5;

    // ── Parameter vector index constants ─────────────────────────────────────────────────────
    // Maps the 7-element parameter array p[] to named model parameters.
    private const int IdxI0Metal    = 0;
    private const int IdxBetaMetal  = 1;
    private const int IdxI0Orr      = 2;
    private const int IdxBetaOrr    = 3;
    private const int IdxIlimOrr    = 4;
    private const int IdxI0Her      = 5;
    private const int IdxBetaHer    = 6;
    private const int NumParams     = 7;

    /// <summary>
    /// Fit the BV model to <paramref name="currentDensityAcm2"/> vs
    /// <paramref name="potentialV"/>, using <paramref name="ecorrHintV"/> as a
    /// starting guess for Ecorr.
    /// </summary>
    /// <param name="potentialV">Potential values (V), sorted ascending.</param>
    /// <param name="currentDensityAcm2">Signed current density (A/cm²) at each potential.</param>
    /// <param name="ecorrHintV">Initial estimate for the corrosion potential (V).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature (°C).</param>
    /// <param name="overrides">
    /// Optional user-specified starting values and per-reaction fix flags.
    /// Pass <c>null</c> to use fully automatic initialisation and unconstrained LM fitting.
    /// </param>
    /// <returns>Fitted model parameters.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the input arrays have different lengths or are empty.
    /// </exception>
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

        // Copy to arrays for fast indexed access.
        double[] e = [.. potentialV];
        double[] i = [.. currentDensityAcm2];

        // ── Step 1: Ecorr from zero-crossing interpolation (for Tafel window selection) ──────
        double ecorr0 = EstimateEcorr(e, i, ecorrHintV);

        // ── Step 2: Metal oxidation — symmetry factor and exchange current density ──────────
        FitMetalOxidation(e, i, ecorr0, out double betaMetal, out double i0Metal);

        // ── Step 3: ORR limiting current, then ORR BV parameters ──────────────────────────
        double ilim0 = EstimateIlimOrr(e, i, ecorr0);
        FitOrrBv(e, i, ecorr0, ilim0, out double betaOrr, out double i0Orr);

        // ── Step 4: HER onset and slope ───────────────────────────────────────────────────────
        FitHer(e, i, ilim0, out double i0Her, out double betaHer);

        // ── Build initial parameter vector and bounds ─────────────────────────────────────────
        double[] p0 =
        [
            i0Metal,
            betaMetal,
            i0Orr,
            betaOrr,
            ilim0,
            i0Her,
            betaHer,
        ];
        double[] lb =
        [
            I0MinAcm2,
            BetaSymMin,
            I0MinAcm2,
            BetaSymMin,
            IlimOrrMinAcm2,
            I0MinAcm2,
            BetaSymMin,
        ];
        double[] ub =
        [
            I0MaxAcm2,
            BetaSymMax,
            I0MaxAcm2,
            BetaSymMax,
            IlimOrrMaxAcm2,
            I0MaxAcm2,
            BetaSymMax,
        ];

        // Clamp initial guess to bounds.
        for (int j = 0; j < NumParams; j++)
            p0[j] = Math.Clamp(p0[j], lb[j], ub[j]);

        // ── Apply user overrides to initial guess and fix flags ───────────────────────────────
        if (overrides != null)
        {
            // Override initial guess values where the user has specified them.
            if (overrides.I0Metal.HasValue)
                p0[IdxI0Metal]   = Math.Clamp(overrides.I0Metal.Value,   I0MinAcm2,     I0MaxAcm2);
            if (overrides.BetaMetal.HasValue)
                p0[IdxBetaMetal] = Math.Clamp(overrides.BetaMetal.Value, BetaSymMin,    BetaSymMax);
            if (overrides.I0Orr.HasValue)
                p0[IdxI0Orr]     = Math.Clamp(overrides.I0Orr.Value,    I0MinAcm2,     I0MaxAcm2);
            if (overrides.BetaOrr.HasValue)
                p0[IdxBetaOrr]   = Math.Clamp(overrides.BetaOrr.Value,  BetaSymMin,    BetaSymMax);
            if (overrides.IlimOrr.HasValue)
                p0[IdxIlimOrr]   = Math.Clamp(overrides.IlimOrr.Value,  IlimOrrMinAcm2, IlimOrrMaxAcm2);
            if (overrides.I0Her.HasValue)
                p0[IdxI0Her]     = Math.Clamp(overrides.I0Her.Value,    I0MinAcm2,     I0MaxAcm2);
            if (overrides.BetaHer.HasValue)
                p0[IdxBetaHer]   = Math.Clamp(overrides.BetaHer.Value,  BetaSymMin,    BetaSymMax);

            // Pin parameters that are flagged as fixed by setting lb = ub = p0.
            // The LM solver clamps every trial step to [lb, ub], so lb == ub == value keeps
            // the parameter frozen throughout the optimisation.
            if (overrides.FixMetal)
            {
                lb[IdxI0Metal]   = ub[IdxI0Metal]   = p0[IdxI0Metal];
                lb[IdxBetaMetal] = ub[IdxBetaMetal] = p0[IdxBetaMetal];
            }
            if (overrides.FixOrr)
            {
                lb[IdxI0Orr]   = ub[IdxI0Orr]   = p0[IdxI0Orr];
                lb[IdxBetaOrr] = ub[IdxBetaOrr] = p0[IdxBetaOrr];
                lb[IdxIlimOrr] = ub[IdxIlimOrr] = p0[IdxIlimOrr];
            }
            if (overrides.FixHer)
            {
                lb[IdxI0Her]   = ub[IdxI0Her]   = p0[IdxI0Her];
                lb[IdxBetaHer] = ub[IdxBetaHer] = p0[IdxBetaHer];
            }
        }

        // ── Step 5: Levenberg-Marquardt polish ────────────────────────────────────────────────
        // Weight each residual by 1 / max(|I|, percentile_20(|I|)) to balance the fit
        // across the large dynamic range of electrochemical currents.
        double[] absI   = [.. i.Select(v => Math.Abs(v))];
        double   p20    = Percentile(absI, 20);
        double[] weight = [.. absI.Select(v => 1.0 / Math.Max(v, p20))];

        double[] pFitted = LevenbergMarquardtSolver.Solve(
            p => ComputeWeightedResiduals(e, i, weight, p),
            p0, lb, ub);

        // ── Step 6: compute Ecorr as the zero-crossing of the fitted model ───────────────────
        BvModelParameters partialModel = ParametersToModel(pFitted);
        double ecorrFitted = FindEcorr(e, partialModel, ecorr0);

        return new BvModelParameters(MetalReaction, OrrReaction, HerReaction)
        {
            I0Metal            = partialModel.I0Metal,
            BetaMetal          = partialModel.BetaMetal,
            EMetalEquilibriumV = partialModel.EMetalEquilibriumV,
            I0Orr              = partialModel.I0Orr,
            BetaOrr            = partialModel.BetaOrr,
            IlimOrr            = partialModel.IlimOrr,
            EorrEquilibriumV   = partialModel.EorrEquilibriumV,
            I0Her              = partialModel.I0Her,
            BetaHer            = partialModel.BetaHer,
            EherEquilibriumV   = partialModel.EherEquilibriumV,
            Ecorr              = ecorrFitted,
        };
    }

    // ── Step implementations ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Estimate Ecorr from a zero-crossing of the current array.
    /// Data are assumed to be sorted ascending by potential on entry.
    /// Falls back to the potential at minimum |I| when no zero crossing is found.
    /// </summary>
    private static double EstimateEcorr(double[] e, double[] i, double hint)
    {
        // First look for a sign change in current.
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

        // Fall back to potential at minimum |I|.
        int minIdx = 0;
        double minAbsI = Math.Abs(i[0]);
        for (int k = 1; k < i.Length; k++)
        {
            double absI = Math.Abs(i[k]);
            if (absI < minAbsI) { minAbsI = absI; minIdx = k; }
        }
        return e[minIdx];
    }

    /// <summary>
    /// Estimates the metal-oxidation Butler-Volmer symmetry factor βₘₑₜₐₗ and exchange current
    /// density I₀,metal by fitting log10(|I|) vs E over the anodic Tafel window
    /// [Ecorr + <see cref="TafelLowerOffsetV"/>, Ecorr + <see cref="TafelUpperOffsetV"/>].
    /// In the anodic Tafel region the reverse (cathodic) metal-oxidation term is negligible,
    /// so OLS regression on the forward exponential yields the symmetry factor and exchange current.
    /// </summary>
    private static void FitMetalOxidation(
        double[] e, double[] i, double ecorr,
        out double betaMetal, out double i0Metal)
    {
        betaMetal = DefaultBetaMetal;
        i0Metal   = DefaultI0MetalAcm2;

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

        // slope = BetaMetal * z * F / (R * T * ln10)
        // → BetaMetal = slope * R * T * ln10 / (z * F)
        double zFoverRTln10 = MetalReaction.Z * ElectrochemicalReaction.F
                              / (ElectrochemicalReaction.R * MetalReaction.TemperatureKelvin * Math.Log(10.0));
        double betaFit = slope / zFoverRTln10;

        // I0Metal = 10^(slope * E_eq_metal + intercept) — extrapolated to the equilibrium potential.
        double eEqMetal   = MetalReaction.EquilibriumPotentialVshe;
        double i0MetalFit = Math.Pow(10.0, slope * eEqMetal + intercept);

        if (double.IsFinite(betaFit) && double.IsFinite(i0MetalFit))
        {
            betaMetal = Math.Clamp(betaFit, BetaSymMin, BetaSymMax);
            i0Metal   = Math.Clamp(i0MetalFit, I0MinAcm2, I0MaxAcm2);
        }
    }

    /// <summary>
    /// Estimates the ORR Butler-Volmer symmetry factor βₒᵣᵣ and exchange current density I₀,ORR
    /// by fitting log10(|I|) vs E over the cathodic Tafel window
    /// [Ecorr − <see cref="TafelUpperOffsetV"/>, Ecorr − <see cref="TafelLowerOffsetV"/>].
    /// In the cathodic Tafel region the anodic ORR term is negligible, so OLS regression on the
    /// forward (cathodic) exponential yields the symmetry factor and exchange current.
    /// </summary>
    private static void FitOrrBv(
        double[] e, double[] i, double ecorr, double ilimOrr,
        out double betaOrr, out double i0Orr)
    {
        betaOrr = DefaultBetaOrr;
        i0Orr   = DefaultI0OrrAcm2;

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

        // In cathodic Tafel region: log|i_kinetic| ≈ log(I0Orr) − (1−β)*z*F/(R*T*ln10) * (E − E_eq_ORR)
        // slope of log|i| vs E = −(1−BetaOrr)*z*F / (R*T*ln10)  [typically negative]
        // → (1−BetaOrr) = −slope * R*T*ln10 / (z*F)
        double zFoverRTln10 = OrrReaction.Z * ElectrochemicalReaction.F
                              / (ElectrochemicalReaction.R * OrrReaction.TemperatureKelvin * Math.Log(10.0));
        double oneMinusBeta = -slope / zFoverRTln10;
        double betaFit      = 1.0 - oneMinusBeta;

        // I0Orr = 10^(slope * E_eq_ORR + intercept) — extrapolated to the equilibrium potential.
        double eEqOrr   = OrrReaction.EquilibriumPotentialVshe;
        double i0OrrFit = Math.Pow(10.0, slope * eEqOrr + intercept);

        if (double.IsFinite(betaFit) && double.IsFinite(i0OrrFit))
        {
            betaOrr = Math.Clamp(betaFit, BetaSymMin, BetaSymMax);
            i0Orr   = Math.Clamp(i0OrrFit, I0MinAcm2, I0MaxAcm2);
        }
    }

    /// <summary>
    /// Estimate the ORR limiting current density as the median of |I| values in the
    /// deepest cathodic <see cref="IlimOrrDepthFraction"/> of the potential range,
    /// where the ORR mass-transport plateau is most clearly visible.
    /// </summary>
    private static double EstimateIlimOrr(double[] e, double[] i, double ecorr)
    {
        if (e.Length == 0)
            return 1e-6;

        double eMin   = e.Min();
        double eRange = e.Max() - eMin;
        double cutoff = eMin + eRange * IlimOrrDepthFraction; // deepest 20 %

        double[] deepI = [.. i
            .Where((_, k) => e[k] <= cutoff && e[k] < ecorr)
            .Select(v => Math.Abs(v))];

        if (deepI.Length == 0)
        {
            // Fall back to 75th percentile of all cathodic |I|.
            double[] cathodicI = [.. i.Where((_, k) => e[k] < ecorr).Select(v => Math.Abs(v))];
            return cathodicI.Length > 0
                ? Math.Clamp(Percentile(cathodicI, 75), IlimOrrMinAcm2, IlimOrrMaxAcm2)
                : 1e-6;
        }

        return Math.Clamp(Median(deepI), IlimOrrMinAcm2, IlimOrrMaxAcm2);
    }

    /// <summary>
    /// Estimate HER exchange current density and symmetry factor by fitting the full
    /// Butler-Volmer cathodic half-reaction in the HER-dominant potential window
    /// [E_eq − 400 mV, E_eq − 20 mV].
    /// Falls back to default values when fewer than <see cref="MinHerPoints"/> are available.
    /// </summary>
    private static void FitHer(
        double[] e, double[] i, double ilimOrr,
        out double i0Her, out double betaHer)
    {
        i0Her   = DefaultI0HerAcm2;
        betaHer = 0.5;   // symmetric transfer as default

        double eEq    = HerReaction.EquilibriumPotentialVshe;
        double eWinHi = eEq - 0.02;   // 20 mV below E_eq: avoid near-equilibrium linear region
        double eWinLo = eEq - 0.40;   // 400 mV below E_eq: HER dominates here

        List<double> eWin = [];
        List<double> iWin = [];
        for (int k = 0; k < e.Length; k++)
        {
            if (e[k] >= eWinLo && e[k] <= eWinHi)
            {
                double iResidual = Math.Abs(i[k]) - ilimOrr;
                if (iResidual > LogFloorAcm2)
                {
                    eWin.Add(e[k]);
                    iWin.Add(-iResidual);   // sign: cathodic = negative
                }
            }
        }

        if (eWin.Count < MinHerPoints)
            return;   // fall back to defaults

        double[] eArr = [.. eWin];
        double[] iArr = [.. iWin];

        // Seed β from the Tafel-slope OLS approach:
        // log|i| = log(i0) − (1−β)·z·F·η / (R·T·ln10)
        double[] eta         = [.. eArr.Select(selector: v => v - eEq)];
        double[] logI        = [.. iArr.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2)))];
        double zFoverRTln10  = HerReaction.Z * ElectrochemicalReaction.F
                               / (ElectrochemicalReaction.R * HerReaction.TemperatureKelvin * Math.Log(10.0));

        if (OlsFit(eta, logI, out double slope, out double intercept))
        {
            // slope = −(1−β) · zF / (R·T·ln10)  →  β = 1 + slope / zFoverRTln10
            double betaSeed = 1.0 + slope / zFoverRTln10;
            double i0Seed   = Math.Pow(10.0, intercept);

            betaHer = Math.Clamp(betaSeed, BetaSymMin, BetaSymMax);
            i0Her   = Math.Clamp(i0Seed,  I0MinAcm2,  I0MaxAcm2);
        }

        // Polish with a bounded two-parameter Levenberg-Marquardt solve.
        double zFoverRT = HerReaction.Z * ElectrochemicalReaction.F
                          / (ElectrochemicalReaction.R * HerReaction.TemperatureKelvin);

        double[] p0Her = { i0Her, betaHer };
        double[] lbHer = { I0MinAcm2, BetaSymMin };
        double[] ubHer = { I0MaxAcm2, BetaSymMax };

        double[] weightHer = [.. iArr.Select(v => 1.0 / Math.Max(Math.Abs(v), 1e-12))];

        double[] pHerFitted = LevenbergMarquardtSolver.Solve(
            p =>
            {
                var residuals = new double[eArr.Length];
                for (int k = 0; k < eArr.Length; k++)
                {
                    double etaK   = eArr[k] - eEq;
                    double iModel = -p[0] * Math.Exp(
                        Math.Clamp(-(1.0 - p[1]) * zFoverRT * etaK, ExpClipMin, ExpClipMax));
                    residuals[k] = (iModel - iArr[k]) * weightHer[k];
                }
                return residuals;
            },
            p0Her, lbHer, ubHer);

        i0Her   = Math.Clamp(pHerFitted[0], I0MinAcm2, I0MaxAcm2);
        betaHer = Math.Clamp(pHerFitted[1], BetaSymMin, BetaSymMax);
    }

    /// <summary>
    /// Finds Ecorr as the zero-crossing of the fitted model's net current density,
    /// using a linear scan followed by linear interpolation over the experimental potential range.
    /// Falls back to <paramref name="ecorrHint"/> if no sign change is found.
    /// </summary>
    private static double FindEcorr(double[] e, BvModelParameters model, double ecorrHint)
    {
        if (e.Length == 0)
            return ecorrHint;

        double eMin = e.Min();
        double eMax = e.Max();

        // Scan for a sign change in the model current over the experimental range.
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

    // ── Mathematical utilities ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluate the BV model defined by <paramref name="p"/> at all potentials in
    /// <paramref name="e"/> and return weighted residuals (model − measured) / weight.
    /// </summary>
    private static double[] ComputeWeightedResiduals(double[] e, double[] iMeasured, double[] weight, double[] p)
    {
        BvModelParameters model = ParametersToModel(p);
        double[] residuals = new double[e.Length];
        for (int k = 0; k < e.Length; k++)
        {
            double iModel = model.ComputeCurrentDensity(e[k]);
            residuals[k] = (iModel - iMeasured[k]) * weight[k];
        }
        return residuals;
    }

    /// <summary>
    /// Convert a raw 7-element parameter vector to a <see cref="BvModelParameters"/> object.
    /// Ecorr defaults to 0 and is set post-fit by <see cref="FindEcorr"/>.
    /// </summary>
    private static BvModelParameters ParametersToModel(double[] p) =>
        new BvModelParameters(MetalReaction, OrrReaction, HerReaction)
        {
            I0Metal            = p[IdxI0Metal],
            BetaMetal          = p[IdxBetaMetal],
            EMetalEquilibriumV = MetalReaction.EquilibriumPotentialVshe,
            I0Orr              = p[IdxI0Orr],
            BetaOrr            = p[IdxBetaOrr],
            IlimOrr            = p[IdxIlimOrr],
            EorrEquilibriumV   = OrrReaction.EquilibriumPotentialVshe,
            I0Her              = p[IdxI0Her],
            BetaHer            = p[IdxBetaHer],
            EherEquilibriumV   = HerReaction.EquilibriumPotentialVshe,
        };

    /// <summary>
    /// Ordinary least-squares fit of y = slope·x + intercept.
    /// Returns <c>false</c> if the regression cannot be computed (e.g., zero variance in x).
    /// </summary>
    private static bool OlsFit(double[] x, double[] y, out double slope, out double intercept)
    {
        slope     = 0.0;
        intercept = 0.0;

        int n = x.Length;
        if (n < 2)
            return false;

        double sumX  = 0.0, sumY  = 0.0, sumXY = 0.0, sumX2 = 0.0;
        foreach (double v in x)  sumX  += v;
        foreach (double v in y)  sumY  += v;
        for (int k = 0; k < n; k++) { sumXY += x[k] * y[k]; sumX2 += x[k] * x[k]; }

        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-30)
            return false;

        slope     = (n * sumXY - sumX * sumY) / denom;
        intercept = (sumY - slope * sumX) / n;
        return true;
    }

    /// <summary>Returns the median of <paramref name="values"/> (does not modify the input).</summary>
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

    /// <summary>
    /// Returns the <paramref name="percentile"/>-th percentile (0–100) of <paramref name="values"/>
    /// using linear interpolation.
    /// </summary>
    private static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
            return 0.0;

        double[] sorted = (double[])values.Clone();
        Array.Sort(sorted);

        double idx = (percentile / 100.0) * (sorted.Length - 1);
        int    lo  = (int)Math.Floor(idx);
        int    hi  = Math.Min(lo + 1, sorted.Length - 1);
        double frac = idx - lo;
        return sorted[lo] * (1.0 - frac) + sorted[hi] * frac;
    }
}
