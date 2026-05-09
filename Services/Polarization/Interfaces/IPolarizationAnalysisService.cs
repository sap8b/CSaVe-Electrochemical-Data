namespace CSaVe_Electrochemical_Data;

/// <summary>
/// Orchestrates the full polarization-curve analysis pipeline:
/// CSV reading → monotonicity filtering → curve joining (if two files) →
/// Ecorr estimation → BV fitting → metric extraction → result assembly.
/// </summary>
public interface IPolarizationAnalysisService
{
    /// <summary>
    /// Run the full analysis described by <paramref name="input"/>.
    /// </summary>
    /// <param name="input">Analysis configuration including file paths, electrode area, and temperature.</param>
    /// <returns>
    /// A <see cref="PolarizationAnalysisResult"/> containing corrosion metrics, fitted BV parameters,
    /// and display-resolution curve data.  <see cref="PolarizationAnalysisResult.Success"/> is
    /// <c>false</c> and <see cref="PolarizationAnalysisResult.Message"/> is populated if the
    /// analysis fails.
    /// </returns>
    PolarizationAnalysisResult Analyse(PolarizationAnalysisInput input);
}
