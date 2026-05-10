using System;
using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data;

/// <summary>Full result returned by <see cref="IPolarizationAnalysisService"/>.</summary>
public sealed class PolarizationAnalysisResult
{
    /// <summary>Indicates whether the analysis completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Human-readable status message; contains error details on failure.</summary>
    public string Message { get; init; } = string.Empty;

    // ── Corrosion metrics ──────────────────────────────────────────────────────────────────────
    /// <summary>Corrosion potential Ecorr (V), derived as the zero-crossing of the fitted model.</summary>
    public double EcorrV { get; init; }

    /// <summary>Corrosion current density icorr (A/cm²), evaluated from the fitted metal-oxidation BV component at Ecorr.</summary>
    public double IcorrAcm2 { get; init; }

    /// <summary>Metal-oxidation BV symmetry factor βₘₑₜₐₗ (dimensionless), converted to effective anodic Tafel slope (V/decade) for display.</summary>
    public double BetaAnodicV { get; init; }

    /// <summary>ORR BV symmetry factor βₒᵣᵣ (dimensionless), converted to effective cathodic Tafel slope (V/decade) for display.</summary>
    public double BetaCathodicV { get; init; }

    /// <summary>ORR limiting current density ilim (A/cm²).</summary>
    public double IlimOrrAcm2 { get; init; }

    /// <summary>HER equilibrium potential (V) fixed by the Nernst equation.</summary>
    public double HerEquilibriumV { get; init; }

    /// <summary>Anodic exchange current density from Tafel back-extrapolation (A/cm²).</summary>
    public double IOxAcm2 { get; init; }

    /// <summary>Fitted metal-oxidation exchange current density I₀,metal (A/cm²).</summary>
    public double I0AnodicAcm2 { get; init; }

    /// <summary>Fitted ORR exchange current density I₀,ORR (A/cm²).</summary>
    public double I0CathodicAcm2 { get; init; }

    /// <summary>Fitted HER symmetry factor βₕₑᵣ (dimensionless).</summary>
    public double BetaHer { get; init; }

    /// <summary>Fitted HER exchange current density I₀,HER (A/cm²).</summary>
    public double I0HerAcm2 { get; init; }

    /// <summary>Estimated ORR boundary-layer thickness (cm).</summary>
    public double BoundaryLayerThicknessCm { get; init; }

    /// <summary>
    /// Current densities (A/cm²) at each requested protection potential.
    /// Keys are the requested potentials in mV formatted as integers (e.g. "-850").
    /// </summary>
    public IReadOnlyDictionary<string, double> ProtectionCurrentDensitiesAcm2 { get; init; }
        = new Dictionary<string, double>();

    // ── Plot data — raw measured curves ──────────────────────────────────────────────────────
    /// <summary>
    /// Electrode potentials (V) for the display-resolution merged curve (both files combined),
    /// sorted ascending.  In single-file mode this equals the full scan.
    /// </summary>
    public IReadOnlyList<double> PlotPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>Measured current density (A/cm²) at each merged plot potential; signed.</summary>
    public IReadOnlyList<double> PlotCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Electrode potentials (V) for the raw anodic-file data only (two-file mode).
    /// Empty in single-file mode.
    /// </summary>
    public IReadOnlyList<double> PlotAnodicFilePotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Measured current density (A/cm²) for the raw anodic-file data only (two-file mode); signed.
    /// Empty in single-file mode.
    /// </summary>
    public IReadOnlyList<double> PlotAnodicFileCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Electrode potentials (V) for the raw cathodic-file data only (two-file mode).
    /// Empty in single-file mode.
    /// </summary>
    public IReadOnlyList<double> PlotCathodicFilePotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Measured current density (A/cm²) for the raw cathodic-file data only (two-file mode); signed.
    /// Empty in single-file mode.
    /// </summary>
    public IReadOnlyList<double> PlotCathodicFileCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    // ── Plot data — fitted model curves ──────────────────────────────────────────────────────
    /// <summary>Potentials (V) used to evaluate the fitted model curves.</summary>
    public IReadOnlyList<double> PlotFitPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>Optional iR-corrected model potential axis (V) for overlay plotting.</summary>
    public IReadOnlyList<double> PlotIrCorrectedPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>BV model total current density (A/cm²) at each model potential; signed.</summary>
    public IReadOnlyList<double> PlotModelCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>
    /// Metal-oxidation Butler-Volmer component current density (A/cm²) at each model potential;
    /// net anodic (positive) minus cathodic (negative) metal-dissolution current.
    /// </summary>
    public IReadOnlyList<double> PlotIMetalBvAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>ORR Butler-Volmer + mass-transport component current density (A/cm²) at each model potential.</summary>
    public IReadOnlyList<double> PlotIorrAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>HER Butler-Volmer component current density (A/cm²) at each model potential.</summary>
    public IReadOnlyList<double> PlotIherAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>Fitted BV model parameters for advanced inspection.</summary>
    public BvModelParameters FittedParameters { get; init; }

    /// <summary>
    /// Weighted root-mean-square error of the fitted BV model vs. the merged polarization
    /// curve, computed with weights 1/max(|i|, 20th-percentile(|i|)) to balance the large
    /// current dynamic range.  Units: A/cm².
    /// </summary>
    public double WeightedRmse { get; init; } = double.NaN;
}
