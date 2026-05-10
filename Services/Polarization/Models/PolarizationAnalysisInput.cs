using System.Collections.Generic;

namespace CSaVe_Electrochemical_Data;

/// <summary>Input parameters for polarization curve analysis.</summary>
public sealed class PolarizationAnalysisInput
{
    /// <summary>
    /// Path to the primary (anodic) CSV file, or to the single combined CSV when
    /// <see cref="CathodicFilePath"/> is null.
    /// </summary>
    public string PrimaryFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional path to a separate cathodic CSV.  When null or empty, single-file
    /// mode is used and both branches are extracted automatically from
    /// <see cref="PrimaryFilePath"/>.
    /// </summary>
    public string CathodicFilePath { get; set; }

    /// <summary>Exposed electrode area in cm². Must be &gt; 0. Default 0.495 cm².</summary>
    public double ExposedAreaCm2 { get; set; } = 0.495;

    /// <summary>
    /// Protection potentials of interest (mV vs. reference).  A current density
    /// will be interpolated at each listed potential and included in the result.
    /// Default: -850 mV and -1050 mV.
    /// </summary>
    public IReadOnlyList<double> ProtectionPotentialsMv { get; set; } =
        new[] { -850.0, -1050.0 };

    /// <summary>Temperature in degrees Celsius used for Butler-Volmer calculations. Default 25 °C.</summary>
    public double TemperatureCelsius { get; set; } = 25.0;

    /// <summary>Optional solution resistance in ohms for iR correction. Default 0 Ω (disabled).</summary>
    public double RSolutionOhm { get; set; } = 0.0;

    /// <summary>
    /// Optional user-specified starting values and per-reaction fix flags for BV curve fitting.
    /// When null, all parameters are initialised automatically and fully optimised by LM.
    /// </summary>
    public BvUserOverrides UserOverrides { get; set; }
}
