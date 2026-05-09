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
    /// <summary>Corrosion potential Ecorr (V).</summary>
    public double EcorrV { get; init; }

    /// <summary>Corrosion current density icorr (A/cm²).</summary>
    public double IcorrAcm2 { get; init; }

    /// <summary>Anodic Tafel slope ba (V/decade).</summary>
    public double BetaAnodicV { get; init; }

    /// <summary>Cathodic Tafel slope bc (V/decade).</summary>
    public double BetaCathodicV { get; init; }

    /// <summary>ORR limiting current density ilim (A/cm²).</summary>
    public double IlimOrrAcm2 { get; init; }

    /// <summary>HER onset potential (V).</summary>
    public double HerOnsetV { get; init; }

    /// <summary>Anodic exchange current density from Tafel back-extrapolation (A/cm²).</summary>
    public double IOxAcm2 { get; init; }

    /// <summary>Fitted anodic exchange current density (A/cm²).</summary>
    public double I0AnodicAcm2 { get; init; }

    /// <summary>Fitted cathodic exchange current density (A/cm²).</summary>
    public double I0CathodicAcm2 { get; init; }

    /// <summary>Fitted HER Tafel slope (V/decade).</summary>
    public double BetaHerV { get; init; }

    /// <summary>Fitted HER exchange current density (A/cm²).</summary>
    public double I0HerAcm2 { get; init; }

    /// <summary>Estimated ORR boundary-layer thickness (cm).</summary>
    public double BoundaryLayerThicknessCm { get; init; }

    /// <summary>
    /// Current densities (A/cm²) at each requested protection potential.
    /// Keys are the requested potentials in mV formatted as integers (e.g. "-850").
    /// </summary>
    public IReadOnlyDictionary<string, double> ProtectionCurrentDensitiesAcm2 { get; init; }
        = new Dictionary<string, double>();

    // ── Plot data (display-resolution merged curve, sorted by potential) ──────────────────────
    /// <summary>Electrode potentials (V) for the display-resolution merged curve, sorted ascending.</summary>
    public IReadOnlyList<double> PlotPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>Measured current density (A/cm²) at each plot potential; signed.</summary>
    public IReadOnlyList<double> PlotCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>Potentials (V) used to evaluate the fitted model curves.</summary>
    public IReadOnlyList<double> PlotFitPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>Optional iR-corrected model potential axis (V) for overlay plotting.</summary>
    public IReadOnlyList<double> PlotIrCorrectedPotentialsV { get; init; } = Array.Empty<double>();

    /// <summary>BV model total current density (A/cm²) at each model potential; signed.</summary>
    public IReadOnlyList<double> PlotModelCurrentDensityAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>Anodic dissolution component current density (A/cm²) at each model potential.</summary>
    public IReadOnlyList<double> PlotIoxAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>ORR component current density (A/cm²) at each model potential.</summary>
    public IReadOnlyList<double> PlotIorrAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>HER component current density (A/cm²) at each model potential.</summary>
    public IReadOnlyList<double> PlotIherAcm2 { get; init; } = Array.Empty<double>();

    /// <summary>Fitted BV model parameters for advanced inspection.</summary>
    public BvModelParameters FittedParameters { get; init; }
}
