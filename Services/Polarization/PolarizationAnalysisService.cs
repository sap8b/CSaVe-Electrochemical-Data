using System;
using System.Collections.Generic;
using System.Linq;

namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Orchestrates the full polarization-curve analysis pipeline:
/// CSV reading → monotonicity filtering → curve joining (if two files) →
/// Ecorr estimation → BV fitting → metric extraction → result assembly.
/// Depends on interface abstractions injected through the constructor (Dependency-Inversion Principle).
/// </summary>
public sealed class PolarizationAnalysisService : IPolarizationAnalysisService
{
    private readonly IPolarizationCsvReader     _csvReader;
    private readonly IMonotonicityFilter        _monotonicityFilter;
    private readonly IPolarizationCurveJoiner   _curveJoiner;
    private readonly IBvCurveFitter             _curveFitter;

    /// <summary>
    /// Initialise the service with all required collaborators.
    /// </summary>
    /// <param name="csvReader">CSV reader for polarization data files.</param>
    /// <param name="monotonicityFilter">Filter that removes the return sweep from potentiodynamic scans.</param>
    /// <param name="curveJoiner">Joiner that merges separately recorded anodic and cathodic branches.</param>
    /// <param name="curveFitter">Butler-Volmer curve fitter.</param>
    public PolarizationAnalysisService(
        IPolarizationCsvReader   csvReader,
        IMonotonicityFilter      monotonicityFilter,
        IPolarizationCurveJoiner curveJoiner,
        IBvCurveFitter           curveFitter)
    {
        _csvReader          = csvReader          ?? throw new ArgumentNullException(nameof(csvReader));
        _monotonicityFilter = monotonicityFilter ?? throw new ArgumentNullException(nameof(monotonicityFilter));
        _curveJoiner        = curveJoiner        ?? throw new ArgumentNullException(nameof(curveJoiner));
        _curveFitter        = curveFitter        ?? throw new ArgumentNullException(nameof(curveFitter));
    }

    /// <summary>
    /// Run the full analysis described by <paramref name="input"/>.
    /// Returns a result with <see cref="PolarizationAnalysisResult.Success"/> = <c>false</c>
    /// and a populated <see cref="PolarizationAnalysisResult.Message"/> on failure.
    /// </summary>
    /// <param name="input">Analysis configuration including file paths, electrode area, and temperature.</param>
    /// <returns>Analysis result containing corrosion metrics and BV fitting output.</returns>
    public PolarizationAnalysisResult Analyse(PolarizationAnalysisInput input)
    {
        try
        {
            return RunAnalysis(input);
        }
        catch (Exception ex)
        {
            return new PolarizationAnalysisResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    /// <summary>
    /// Internal implementation of the full analysis pipeline.
    /// Throws on any error; the public <see cref="Analyse"/> wrapper catches and converts to a failure result.
    /// </summary>
    private PolarizationAnalysisResult RunAnalysis(PolarizationAnalysisInput input)
    {
        if (string.IsNullOrWhiteSpace(input.PrimaryFilePath))
            throw new ArgumentException("PrimaryFilePath must be specified.", nameof(input));
        if (input.ExposedAreaCm2 <= 0)
            throw new ArgumentException("ExposedAreaCm2 must be > 0.", nameof(input));

        // ── Step 1: read CSV(s) and apply monotonicity filter ─────────────────────────────
        IReadOnlyList<PolarizationPoint> fitPoints;
        IReadOnlyList<PolarizationPoint> displayPoints;

        bool twoFileMode = !string.IsNullOrWhiteSpace(input.CathodicFilePath);

        if (twoFileMode)
        {
            // Two-file mode: separate anodic and cathodic experiments.
            IReadOnlyList<PolarizationPoint> rawAnodic   = _csvReader.Read(input.PrimaryFilePath);
            IReadOnlyList<PolarizationPoint> rawCathodic = _csvReader.Read(input.CathodicFilePath);

            IReadOnlyList<PolarizationPoint> fwdAnodic   = _monotonicityFilter.Filter(rawAnodic,   isAnodic: true);
            IReadOnlyList<PolarizationPoint> fwdCathodic = _monotonicityFilter.Filter(rawCathodic, isAnodic: false);

            fitPoints = _curveJoiner.Join(fwdAnodic, fwdCathodic);

            // Display curve: all raw points sorted by potential (shows hysteresis loop).
            var allDisplay = new List<PolarizationPoint>(rawAnodic.Count + rawCathodic.Count);
            allDisplay.AddRange(rawAnodic);
            allDisplay.AddRange(rawCathodic);
            allDisplay.Sort((a, b) => a.PotentialV.CompareTo(b.PotentialV));
            displayPoints = allDisplay;
        }
        else
        {
            // Single-file mode: combined sweep already in one CSV.
            IReadOnlyList<PolarizationPoint> rawPoints = _csvReader.Read(input.PrimaryFilePath);

            // Sort all points by potential for a monotonic axis.
            var sorted = rawPoints.OrderBy(p => p.PotentialV).ToList();

            fitPoints    = sorted;
            displayPoints = sorted;
        }

        if (input.RSolutionOhm > 0.0)
            fitPoints = ApplyIrCorrection(fitPoints, input.ExposedAreaCm2, input.RSolutionOhm);

        // ── Step 2: estimate Ecorr as the initial hint ────────────────────────────────────
        double[] ePot    = fitPoints.Select(p => p.PotentialV).ToArray();
        double[] iCurr   = fitPoints.Select(p => p.CurrentA).ToArray();
        double   ecorrHint = EstimateEcorr(ePot, iCurr);

        // ── Step 3: convert raw current to current density ────────────────────────────────
        double   area        = input.ExposedAreaCm2;
        double[] iDensity    = fitPoints.Select(p => p.CurrentA / area).ToArray();

        double[] ePotDisplay = displayPoints.Select(p => p.PotentialV).ToArray();
        double[] iDensDisp   = displayPoints.Select(p => p.CurrentA / area).ToArray();

        // ── Step 4: BV fitting ────────────────────────────────────────────────────────────
        BvModelParameters fitted = _curveFitter.Fit(
            ePot.ToList(), iDensity.ToList(), ecorrHint, input.TemperatureCelsius);

        // ── Step 5: compute display-resolution model curves ───────────────────────────────
        double[] ePotFit = fitPoints.Select(p => p.PotentialV).ToArray();
        double[] ePotModel = ePotFit;
        double[] ePotIrCorrected = Array.Empty<double>();

        if (input.RSolutionOhm > 0.0)
        {
            double areaR = area * input.RSolutionOhm;
            ePotIrCorrected = ePotDisplay
                .Select(e => SolveIrCorrectedPotential(e, fitted, areaR))
                .ToArray();
            ePotModel = ePotIrCorrected;
        }

        double[] modelCurve = ePotModel.Select(e => fitted.ComputeCurrentDensity(e)).ToArray();
        double[] iOxCurve   = ePotModel.Select(e => fitted.ComputeAnodicComponent(e)).ToArray();
        double[] iOrrCurve  = ePotModel.Select(e => fitted.ComputeOrrComponent(e)).ToArray();
        double[] iHerCurve  = ePotModel.Select(e => fitted.ComputeHerComponent(e)).ToArray();

        // ── Step 6: extract corrosion metrics from fitted model ───────────────────────────
        double ecorrV    = fitted.Ecorr;
        // icorr: the anodic exchange current density I0Anodic from the Tafel back-extrapolation
        // to Ecorr is the standard electrochemical definition used in polarization curve analysis.
        double icorrAcm2 = fitted.I0Anodic;
        double iOxAcm2   = ComputeIOx(ePot, iDensity, ecorrV);

        // ── Step 7: interpolate protection current densities ─────────────────────────────
        var protectionCurrents = new Dictionary<string, double>();
        foreach (double mv in input.ProtectionPotentialsMv)
        {
            double targetV    = mv / 1000.0;
            double interpI    = InterpolateAbsCurrentDensity(ePotDisplay, iDensDisp, targetV);
            protectionCurrents[((int)mv).ToString()] = interpI;
        }

        // ── Step 8: assemble result ───────────────────────────────────────────────────────
        return new PolarizationAnalysisResult
        {
            Success      = true,
            Message      = $"Polarization analysis completed ({(twoFileMode ? "two-file" : "single-file")} mode).",
            EcorrV       = ecorrV,
            IcorrAcm2    = icorrAcm2,
            BetaAnodicV  = fitted.BetaAnodic,
            BetaCathodicV = fitted.BetaCathodic,
            IlimOrrAcm2  = fitted.IlimOrr,
            HerOnsetV    = fitted.EherOnset,
            IOxAcm2      = iOxAcm2,
            I0AnodicAcm2 = fitted.I0Anodic,
            I0CathodicAcm2 = fitted.I0Cathodic,
            BetaHerV = fitted.BetaHer,
            I0HerAcm2 = fitted.I0Her,
            BoundaryLayerThicknessCm = fitted.IlimOrr > 0.0
                ? (4.0 * 96485.0 * 1.8e-5 * 2.4e-7) / fitted.IlimOrr
                : double.NaN,

            ProtectionCurrentDensitiesAcm2 = protectionCurrents,

            PlotPotentialsV              = ePotDisplay,
            PlotCurrentDensityAcm2       = iDensDisp,
            PlotFitPotentialsV           = ePotFit,
            PlotIrCorrectedPotentialsV   = ePotIrCorrected,
            PlotModelCurrentDensityAcm2  = modelCurve,
            PlotIoxAcm2                  = iOxCurve,
            PlotIorrAcm2                 = iOrrCurve,
            PlotIherAcm2                 = iHerCurve,

            FittedParameters = fitted,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies direct iR correction to measured polarization points using E_corrected = E_measured − I·R.
    /// </summary>
    private static IReadOnlyList<PolarizationPoint> ApplyIrCorrection(
        IReadOnlyList<PolarizationPoint> points,
        double exposedAreaCm2,
        double rSolutionOhm)
    {
        if (rSolutionOhm <= 0.0)
            return points;

        double areaR = exposedAreaCm2 * rSolutionOhm;
        return points
            .Select(p => new PolarizationPoint
            {
                PotentialV = p.PotentialV - p.CurrentDensityAcm2(exposedAreaCm2) * areaR,
                CurrentA = p.CurrentA
            })
            .OrderBy(p => p.PotentialV)
            .ToArray();
    }

    /// <summary>
    /// Solves for the iR-corrected potential E_true corresponding to an apparent potential E_apparent.
    /// </summary>
    private static double SolveIrCorrectedPotential(double eApparent, BvModelParameters model, double areaR)
    {
        if (areaR <= 0.0)
            return eApparent;

        // A small derivative step keeps the numerical slope stable over clipped exponentials,
        // while the tolerance/iteration cap are tight enough for sub-microvolt convergence
        // without risking long-running iterations on nearly flat regions.
        const int maxIterations = 25;
        const double toleranceV = 1e-10;
        const double derivativeStepV = 1e-6;

        double e = eApparent;
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            double iModel = model.ComputeCurrentDensity(e);
            double residual = e + iModel * areaR - eApparent;
            if (Math.Abs(residual) <= toleranceV)
                return e;

            double iForward = model.ComputeCurrentDensity(e + derivativeStepV);
            double iBackward = model.ComputeCurrentDensity(e - derivativeStepV);
            double diDe = (iForward - iBackward) / (2.0 * derivativeStepV);
            double derivative = 1.0 + diDe * areaR;
            if (!double.IsFinite(derivative) || Math.Abs(derivative) < 1e-12)
                break;

            double next = e - residual / derivative;
            if (!double.IsFinite(next))
                break;

            if (Math.Abs(next - e) <= toleranceV)
                return next;

            e = next;
        }

        return e;
    }

    /// <summary>
    /// Estimate Ecorr from the zero-crossing of the current array (sorted ascending by potential).
    /// Falls back to the potential at minimum |I| if no zero crossing is found.
    /// </summary>
    /// <param name="e">Potential values (V), sorted ascending.</param>
    /// <param name="i">Signed current density (A/cm²) values.</param>
    /// <returns>Estimated Ecorr (V).</returns>
    private static double EstimateEcorr(double[] e, double[] i)
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

        int    minIdx  = 0;
        double minAbsI = Math.Abs(i[0]);
        for (int k = 1; k < i.Length; k++)
        {
            double absI = Math.Abs(i[k]);
            if (absI < minAbsI) { minAbsI = absI; minIdx = k; }
        }
        return e[minIdx];
    }

    /// <summary>
    /// Compute the anodic exchange current density i_ox via Tafel-region back-extrapolation
    /// to Ecorr.  Fits log10(|I|) vs E over [Ecorr + 0.01 V, Ecorr + 0.15 V].
    /// Returns <see cref="double.NaN"/> if fewer than 3 points are available.
    /// </summary>
    /// <param name="e">Potential values (V), sorted ascending.</param>
    /// <param name="i">Signed current density (A/cm²) values.</param>
    /// <param name="ecorr">Fitted corrosion potential (V).</param>
    /// <returns>Back-extrapolated i_ox (A/cm²), or NaN if the window is too small.</returns>
    private static double ComputeIOx(double[] e, double[] i, double ecorr)
    {
        const double lower = 0.01;
        const double upper = 0.15;
        const double logFloor = 1e-20;

        double[] eWin = e.Where((_, k) => e[k] >= ecorr + lower && e[k] <= ecorr + upper).ToArray();
        double[] iWin = i.Where((_, k) => e[k] >= ecorr + lower && e[k] <= ecorr + upper).ToArray();

        if (eWin.Length < 3)
            return double.NaN;

        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        int n = eWin.Length;
        for (int k = 0; k < n; k++)
        {
            double logI = Math.Log10(Math.Max(Math.Abs(iWin[k]), logFloor));
            sumX  += eWin[k];
            sumY  += logI;
            sumXY += eWin[k] * logI;
            sumX2 += eWin[k] * eWin[k];
        }

        double denom = n * sumX2 - sumX * sumX;
        if (Math.Abs(denom) < 1e-30)
            return double.NaN;

        double slope = (n * sumXY - sumX * sumY) / denom;
        double intcp = (sumY - slope * sumX) / n;
        return Math.Pow(10.0, slope * ecorr + intcp);
    }

    /// <summary>
    /// Interpolate |I| at <paramref name="targetV"/> using the sorted
    /// (potential, |current density|) curve.  Clamps to the boundary values outside the range.
    /// </summary>
    /// <param name="e">Potential values (V), sorted ascending.</param>
    /// <param name="i">Signed current density (A/cm²) values.</param>
    /// <param name="targetV">Target potential (V).</param>
    /// <returns>Interpolated |I| (A/cm²).</returns>
    private static double InterpolateAbsCurrentDensity(double[] e, double[] i, double targetV)
    {
        double[] absI = i.Select(v => Math.Abs(v)).ToArray();

        if (targetV <= e[0])
            return absI[0];
        if (targetV >= e[e.Length - 1])
            return absI[e.Length - 1];

        // Binary search for the bracketing interval.
        int lo = 0, hi = e.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (e[mid] <= targetV) lo = mid; else hi = mid;
        }

        double t = (targetV - e[lo]) / (e[hi] - e[lo]);
        return absI[lo] * (1.0 - t) + absI[hi] * t;
    }
}
