using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Fits the 11-parameter Butler-Volmer model to a merged polarization curve using a
/// multi-step initialisation followed by a Levenberg-Marquardt global polish.
/// All arithmetic uses <see cref="System.Math"/> only; no external libraries are required.
/// </summary>
public sealed class BvCurveFitter : IBvCurveFitter
{
    // ── Tafel window offsets relative to Ecorr ────────────────────────────────────────────────
    // Lower offset: skip the near-linear mixed-potential zone where neither branch is dominant.
    private const double TafelLowerOffsetV       = 0.01;   // 0.01 V below/above Ecorr

    // Upper offset: stay within the true Tafel region before diffusion effects set in.
    private const double TafelUpperOffsetV       = 0.15;   // 0.15 V below/above Ecorr

    // ── Default fall-back values when regression windows have too few points ──────────────────
    // Default anodic Tafel slope (V/decade) for steel in near-neutral chloride.
    private const double DefaultBaV              = 0.060;

    // Default anodic exchange current density (A/cm²) – representative of mild steel at OCP.
    private const double DefaultI0aAcm2          = 1e-8;

    // Default cathodic Tafel slope (V/decade) – typical for ORR activation on steel.
    private const double DefaultBcV              = 0.100;

    // Default cathodic exchange current density (A/cm²).
    private const double DefaultI0cFraction      = 0.5;    // fraction of max cathodic |I|

    // Default HER Tafel slope (V/decade).
    private const double DefaultBHerV            = 0.120;

    // Default HER exchange current density (A/cm²).
    private const double DefaultI0HerAcm2        = 1e-9;

    // Default HER onset offset below Ecorr (V).
    private const double DefaultEHerOffsetV      = 0.30;

    // ── Fitted-parameter box bounds ───────────────────────────────────────────────────────────
    // Minimum physically meaningful exchange current density (A/cm²).
    private const double I0MinAcm2               = 1e-12;

    // Maximum exchange current density (A/cm²) before the solution is non-physical.
    private const double I0MaxAcm2               = 1e-1;

    // Minimum Tafel slope (V/decade) – sharp activation.
    private const double BetaMinV                = 0.01;

    // Maximum Tafel slope (V/decade) – very sluggish kinetics.
    private const double BetaMaxV                = 0.50;

    // Maximum ORR limiting current density (A/cm²) – generous upper bound.
    private const double IlimOrrMaxAcm2          = 1.0;

    // Minimum ORR limiting current density (A/cm²).
    private const double IlimOrrMinAcm2          = 1e-10;

    // Ecorr lower bound offset: allow Ecorr to move 100 mV below the hint.
    private const double EcorrLowerOffsetV       = 0.10;

    // Ecorr upper bound offset: allow Ecorr to move 50 mV above the hint.
    private const double EcorrUpperOffsetV       = 0.05;

    // EorrTransition absolute lower / upper bounds (V).
    private const double EorrTransitionLowerV    = -2.0;
    private const double EorrTransitionUpperV    =  0.2;

    // WorrV (sigmoid width) bounds (V).
    private const double WorrMinV                = 0.005;
    private const double WorrMaxV                = 0.20;
    private const double WorrDefault             = 0.04;   // typical sigmoidal width for ORR

    // EherOnset absolute lower bound (V).
    private const double EherOnsetLowerV         = -2.0;

    // EherOnset upper bound offset below Ecorr (V).
    private const double EherOnsetUpperOffsetV   = 0.01;

    // Offset used when selecting EorrTransition candidates (V below Ecorr).
    private const double EorrSelectionOffsetV    = 0.05;

    // Fall-back EorrTransition offset below Ecorr (V).
    private const double EorrFallbackOffsetV     = 0.10;

    // Lowest cathodic potential fraction for ilim_orr estimation (bottom 20 % of range).
    // The ORR plateau is most clearly visible in the deepest cathodic region.
    private const double IlimOrrDepthFraction    = 0.20;

    // Offset below Ecorr used to define the HER-dominant region.
    private const double HerRegionOffsetV        = 0.25;

    // Floor applied before log10 to prevent log(0) errors.
    private const double LogFloorAcm2            = 1e-20;

    // Minimum number of Tafel-region points required before running OLS regression.
    private const int MinTafelPoints             = 2;

    // Minimum number of HER-region points required before running HER regression.
    private const int MinHerPoints               = 5;

    // ── Parameter vector index constants ─────────────────────────────────────────────────────
    // Maps the 11-element parameter array p[] to named model parameters.
    private const int IdxI0Anodic      = 0;
    private const int IdxBetaAnodic    = 1;
    private const int IdxI0Cathodic    = 2;
    private const int IdxBetaCathodic  = 3;
    private const int IdxEcorr         = 4;
    private const int IdxIlimOrr       = 5;
    private const int IdxEorrTransition = 6;
    private const int IdxWorrV         = 7;
    private const int IdxI0Her         = 8;
    private const int IdxBetaHer       = 9;
    private const int IdxEherOnset     = 10;
    private const int NumParams        = 11;

    /// <summary>
    /// Fit the BV model to <paramref name="currentDensityAcm2"/> vs
    /// <paramref name="potentialV"/>, using <paramref name="ecorrHintV"/> as a
    /// starting guess for Ecorr.
    /// </summary>
    /// <param name="potentialV">Potential values (V), sorted ascending.</param>
    /// <param name="currentDensityAcm2">Signed current density (A/cm²) at each potential.</param>
    /// <param name="ecorrHintV">Initial estimate for the corrosion potential (V).</param>
    /// <param name="temperatureCelsius">Electrolyte temperature (°C).</param>
    /// <returns>Fitted model parameters.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the input arrays have different lengths or are empty.
    /// </exception>
    public BvModelParameters Fit(
        IReadOnlyList<double> potentialV,
        IReadOnlyList<double> currentDensityAcm2,
        double ecorrHintV,
        double temperatureCelsius)
    {
        if (potentialV.Count != currentDensityAcm2.Count)
            throw new ArgumentException("potentialV and currentDensityAcm2 must have the same length.");
        if (potentialV.Count == 0)
            throw new ArgumentException("Input arrays must not be empty.");

        // Copy to arrays for fast indexed access.
        double[] e = potentialV.ToArray();
        double[] i = currentDensityAcm2.ToArray();

        // ── Step 1: Ecorr from zero-crossing interpolation ────────────────────────────────
        double ecorr0 = EstimateEcorr(e, i, ecorrHintV);
        int    idxEcorr = IndexOfNearest(e, ecorr0);

        // ── Step 2: Anodic Tafel slope and i0_anodic ─────────────────────────────────────
        FitAnodicTafel(e, i, ecorr0, out double ba, out double i0a);

        // ── Step 3: Cathodic Tafel slope and i0_cathodic ─────────────────────────────────
        FitCathodicTafel(e, i, ecorr0, out double bc, out double i0c);

        // ── Step 4: ORR limiting current (ilim_orr) ───────────────────────────────────────
        double ilim0 = EstimateIlimOrr(e, i, ecorr0);

        // ── Step 5: HER onset and slope ───────────────────────────────────────────────────
        FitHer(e, i, ecorr0, ilim0, out double i0Her, out double bHer, out double eHer);

        // ── Build initial parameter vector and bounds ─────────────────────────────────────
        double[] eorrCandidates = e.Where(v => v < ecorr0 - EorrSelectionOffsetV).ToArray();
        double eorr0 = eorrCandidates.Length >= 3
            ? Median(eorrCandidates)
            : ecorr0 - EorrFallbackOffsetV;

        double[] p0 = new double[NumParams];
        p0[IdxI0Anodic]       = i0a;
        p0[IdxBetaAnodic]     = ba;
        p0[IdxI0Cathodic]     = i0c;
        p0[IdxBetaCathodic]   = bc;
        p0[IdxEcorr]          = ecorr0;
        p0[IdxIlimOrr]        = ilim0;
        p0[IdxEorrTransition] = eorr0;
        p0[IdxWorrV]          = WorrDefault;
        p0[IdxI0Her]          = i0Her;
        p0[IdxBetaHer]        = bHer;
        p0[IdxEherOnset]      = eHer;

        double[] lb = new double[NumParams];
        lb[IdxI0Anodic]       = I0MinAcm2;
        lb[IdxBetaAnodic]     = BetaMinV;
        lb[IdxI0Cathodic]     = I0MinAcm2;
        lb[IdxBetaCathodic]   = BetaMinV;
        lb[IdxEcorr]          = ecorrHintV - EcorrLowerOffsetV;
        lb[IdxIlimOrr]        = IlimOrrMinAcm2;
        lb[IdxEorrTransition] = EorrTransitionLowerV;
        lb[IdxWorrV]          = WorrMinV;
        lb[IdxI0Her]          = I0MinAcm2;
        lb[IdxBetaHer]        = BetaMinV;
        lb[IdxEherOnset]      = EherOnsetLowerV;

        double[] ub = new double[NumParams];
        ub[IdxI0Anodic]       = I0MaxAcm2;
        ub[IdxBetaAnodic]     = BetaMaxV;
        ub[IdxI0Cathodic]     = I0MaxAcm2;
        ub[IdxBetaCathodic]   = BetaMaxV;
        ub[IdxEcorr]          = ecorrHintV + EcorrUpperOffsetV;
        ub[IdxIlimOrr]        = IlimOrrMaxAcm2;
        ub[IdxEorrTransition] = EorrTransitionUpperV;
        ub[IdxWorrV]          = WorrMaxV;
        ub[IdxI0Her]          = I0MaxAcm2;
        ub[IdxBetaHer]        = BetaMaxV;
        ub[IdxEherOnset]      = ecorrHintV - EherOnsetUpperOffsetV;

        // Clamp initial guess to bounds.
        for (int j = 0; j < NumParams; j++)
            p0[j] = Math.Clamp(p0[j], lb[j], ub[j]);

        // ── Step 6: Levenberg-Marquardt polish ────────────────────────────────────────────
        // Weight each residual by 1 / max(|I|, percentile_20(|I|)) to balance the fit
        // across the large dynamic range of electrochemical currents.
        double[] absI   = i.Select(v => Math.Abs(v)).ToArray();
        double   p20    = Percentile(absI, 20);
        double[] weight = absI.Select(v => 1.0 / Math.Max(v, p20)).ToArray();

        double[] pFitted = LevenbergMarquardtSolver.Solve(
            p => ComputeWeightedResiduals(e, i, weight, p),
            p0, lb, ub);

        return ParametersToModel(pFitted);
    }

    // ── Step implementations ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Estimate Ecorr from a zero-crossing of the current array.
    /// Data are assumed to be sorted ascending by potential on entry.
    /// Falls back to the potential at minimum |I| when no zero crossing is found.
    /// </summary>
    private static double EstimateEcorr(double[] e, double[] i, double hint)
    {
        // Use the hint directly if it is well inside the potential range.
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
    /// Fit log10(|I|) vs E by ordinary least squares over the anodic Tafel window
    /// [Ecorr + <see cref="TafelLowerOffsetV"/>, Ecorr + <see cref="TafelUpperOffsetV"/>].
    /// Sets <paramref name="ba"/> (V/decade) and <paramref name="i0a"/> (A/cm²).
    /// </summary>
    private static void FitAnodicTafel(
        double[] e, double[] i, double ecorr,
        out double ba, out double i0a)
    {
        ba  = DefaultBaV;
        i0a = DefaultI0aAcm2;

        double eMin = ecorr + TafelLowerOffsetV;
        double eMax = ecorr + TafelUpperOffsetV;
        double[] eWin = e.Where((_, k) => e[k] >= eMin && e[k] <= eMax).ToArray();
        double[] iWin = i.Where((_, k) => e[k] >= eMin && e[k] <= eMax).ToArray();

        if (eWin.Length < MinTafelPoints)
            return;

        double[] logI = iWin.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2))).ToArray();

        if (!OlsFit(eWin, logI, out double slope, out double intercept))
            return;

        if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-30)
            return;

        double baFit  = 1.0 / (Math.Abs(slope) * Math.Log(10.0));
        double i0aFit = Math.Pow(10.0, slope * ecorr + intercept);

        if (double.IsFinite(baFit)  && double.IsFinite(i0aFit))
        {
            ba  = Math.Clamp(baFit,  BetaMinV,   BetaMaxV);
            i0a = Math.Clamp(i0aFit, I0MinAcm2, I0MaxAcm2);
        }
    }

    /// <summary>
    /// Fit log10(|I|) vs E by OLS over the cathodic Tafel window
    /// [Ecorr − <see cref="TafelUpperOffsetV"/>, Ecorr − <see cref="TafelLowerOffsetV"/>].
    /// Sets <paramref name="bc"/> (V/decade) and <paramref name="i0c"/> (A/cm²).
    /// </summary>
    private static void FitCathodicTafel(
        double[] e, double[] i, double ecorr,
        out double bc, out double i0c)
    {
        bc  = DefaultBcV;

        double iAbsMax  = i.Select(v => Math.Abs(v)).DefaultIfEmpty(1e-10).Max();
        i0c = Math.Clamp(iAbsMax * DefaultI0cFraction, I0MinAcm2, I0MaxAcm2);

        double eMin = ecorr - TafelUpperOffsetV;
        double eMax = ecorr - TafelLowerOffsetV;
        double[] eWin = e.Where((_, k) => e[k] >= eMin && e[k] <= eMax).ToArray();
        double[] iWin = i.Where((_, k) => e[k] >= eMin && e[k] <= eMax).ToArray();

        if (eWin.Length < MinTafelPoints)
            return;

        double[] logI = iWin.Select(v => Math.Log10(Math.Max(Math.Abs(v), LogFloorAcm2))).ToArray();

        if (!OlsFit(eWin, logI, out double slope, out double intercept))
            return;

        if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-30)
            return;

        double bcFit  = 1.0 / (Math.Abs(slope) * Math.Log(10.0));
        double i0cFit = Math.Pow(10.0, slope * ecorr + intercept);

        if (double.IsFinite(bcFit) && double.IsFinite(i0cFit))
        {
            bc  = Math.Clamp(bcFit,  BetaMinV,   BetaMaxV);
            i0c = Math.Clamp(i0cFit, I0MinAcm2, I0MaxAcm2);
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

        double[] deepI = i
            .Where((_, k) => e[k] <= cutoff && e[k] < ecorr)
            .Select(v => Math.Abs(v))
            .ToArray();

        if (deepI.Length == 0)
        {
            // Fall back to 75th percentile of all cathodic |I|.
            double[] cathodicI = i.Where((_, k) => e[k] < ecorr).Select(v => Math.Abs(v)).ToArray();
            return cathodicI.Length > 0
                ? Math.Clamp(Percentile(cathodicI, 75), IlimOrrMinAcm2, IlimOrrMaxAcm2)
                : 1e-6;
        }

        return Math.Clamp(Median(deepI), IlimOrrMinAcm2, IlimOrrMaxAcm2);
    }

    /// <summary>
    /// Estimate HER onset potential and Tafel slope by fitting log10(|I|) vs E in
    /// the cathodic region below (Ecorr − <see cref="HerRegionOffsetV"/>).
    /// Falls back to default values when fewer than <see cref="MinHerPoints"/> are available.
    /// </summary>
    private static void FitHer(
        double[] e, double[] i, double ecorr, double ilimOrr,
        out double i0Her, out double bHer, out double eHer)
    {
        i0Her = DefaultI0HerAcm2;
        bHer  = DefaultBHerV;
        eHer  = ecorr - DefaultEHerOffsetV;

        double eMax = ecorr - HerRegionOffsetV;
        double[] eWin = e.Where((_, k) => e[k] < eMax).ToArray();
        double[] iWin = i.Where((_, k) => e[k] < eMax).ToArray();

        if (eWin.Length < MinHerPoints)
            return;

        // Subtract the ORR limiting current contribution before fitting the HER slope.
        double[] iResidual = iWin
            .Select(v => Math.Max(Math.Abs(v) - ilimOrr, LogFloorAcm2))
            .ToArray();
        double[] logI = iResidual.Select(v => Math.Log10(v)).ToArray();

        if (!OlsFit(eWin, logI, out double slope, out double intercept))
            return;

        if (!double.IsFinite(slope) || Math.Abs(slope) < 1e-30)
            return;

        double bHerFit  = 1.0 / (Math.Abs(slope) * Math.Log(10.0));
        double i0HerFit = Math.Pow(10.0, slope * ecorr + intercept);
        double eHerFit  = Median(eWin);

        if (double.IsFinite(bHerFit) && double.IsFinite(i0HerFit) && double.IsFinite(eHerFit))
        {
            bHer  = Math.Clamp(bHerFit,  BetaMinV,   BetaMaxV);
            i0Her = Math.Clamp(i0HerFit, I0MinAcm2, I0MaxAcm2);
            eHer  = eHerFit;
        }
    }

    // ── Mathematical utilities ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Evaluate the BV model defined by <paramref name="p"/> at all potentials in
    /// <paramref name="e"/> and return weighted residuals (model − measured) / weight.
    /// </summary>
    private static double[] ComputeWeightedResiduals(double[] e, double[] iMeasured, double[] weight, double[] p)
    {
        var model = ParametersToModel(p);
        double[] residuals = new double[e.Length];
        for (int k = 0; k < e.Length; k++)
        {
            double iModel = model.ComputeCurrentDensity(e[k]);
            residuals[k] = (iModel - iMeasured[k]) * weight[k];
        }
        return residuals;
    }

    /// <summary>
    /// Convert a raw 11-element parameter vector to a <see cref="BvModelParameters"/> object.
    /// </summary>
    private static BvModelParameters ParametersToModel(double[] p) =>
        new BvModelParameters
        {
            I0Anodic      = p[IdxI0Anodic],
            BetaAnodic    = p[IdxBetaAnodic],
            I0Cathodic    = p[IdxI0Cathodic],
            BetaCathodic  = p[IdxBetaCathodic],
            Ecorr         = p[IdxEcorr],
            IlimOrr       = p[IdxIlimOrr],
            EorrTransition = p[IdxEorrTransition],
            WorrV         = p[IdxWorrV],
            I0Her         = p[IdxI0Her],
            BetaHer       = p[IdxBetaHer],
            EherOnset     = p[IdxEherOnset],
        };

    /// <summary>
    /// Ordinary least-squares fit of y = slope·x + intercept.
    /// Returns <c>false</c> if the regression cannot be computed (e.g., zero variance in x).
    /// </summary>
    /// <param name="x">Independent variable values.</param>
    /// <param name="y">Dependent variable values.</param>
    /// <param name="slope">Fitted slope.</param>
    /// <param name="intercept">Fitted intercept.</param>
    /// <returns><c>true</c> if the fit succeeded; <c>false</c> otherwise.</returns>
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

    /// <summary>Returns the index of the element in <paramref name="arr"/> nearest to <paramref name="target"/>.</summary>
    private static int IndexOfNearest(double[] arr, double target)
    {
        int    best    = 0;
        double bestDist = Math.Abs(arr[0] - target);
        for (int k = 1; k < arr.Length; k++)
        {
            double d = Math.Abs(arr[k] - target);
            if (d < bestDist) { bestDist = d; best = k; }
        }
        return best;
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
