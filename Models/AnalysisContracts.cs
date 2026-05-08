using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CSaVe_Electrochemical_Data.Models;

public sealed class PolarizationAnalysisRequest
{
    [JsonPropertyName("anodic_file")]
    public string Anodic_File { get; set; } = string.Empty;
    [JsonPropertyName("cathodic_file")]
    public string Cathodic_File { get; set; } = string.Empty;
    [JsonPropertyName("exposed_area_cm2")]
    public double Exposed_Area_Cm2 { get; set; } = 0.495;
    [JsonPropertyName("protection_potentials_mv")]
    public List<double> Protection_Potentials_Mv { get; set; } = new() { -850.0, -1050.0 };
}

public sealed class EisAnalysisRequest
{
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
    [JsonPropertyName("model")]
    public string Model { get; set; } = "randles_cpe_w";
}

public class AnalysisResponseBase
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class PolarizationAnalysisResponse : AnalysisResponseBase
{
    [JsonPropertyName("files")]
    public List<PolarizationFileResult> Files { get; set; } = new();
    [JsonPropertyName("summary")]
    public Dictionary<string, object> Summary { get; set; } = new();
}

public sealed class PolarizationFileResult
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;
    [JsonPropertyName("metrics")]
    public Dictionary<string, double> Metrics { get; set; } = new();
    [JsonPropertyName("fit_parameters")]
    public Dictionary<string, double> Fit_Parameters { get; set; } = new();
    [JsonPropertyName("plot")]
    public PolarizationPlotData Plot { get; set; } = new();
}

public sealed class PolarizationPlotData
{
    [JsonPropertyName("potential_v")]
    public List<double> Potential_V { get; set; } = new();
    [JsonPropertyName("current_density_a_cm2")]
    public List<double> Current_Density_A_Cm2 { get; set; } = new();
    [JsonPropertyName("model_current_density_a_cm2")]
    public List<double> Model_Current_Density_A_Cm2 { get; set; } = new();
    [JsonPropertyName("i_ox_curve_a_cm2")]
    public List<double> I_Ox_Curve_A_Cm2 { get; set; } = new();
    [JsonPropertyName("i_orr_curve_a_cm2")]
    public List<double> I_Orr_Curve_A_Cm2 { get; set; } = new();
    [JsonPropertyName("i_her_curve_a_cm2")]
    public List<double> I_Her_Curve_A_Cm2 { get; set; } = new();
}

public sealed class EisAnalysisResponse : AnalysisResponseBase
{
    [JsonPropertyName("files")]
    public List<EisFileResult> Files { get; set; } = new();
}

public sealed class EisFileResult
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;
    [JsonPropertyName("fit_parameters")]
    public Dictionary<string, double> Fit_Parameters { get; set; } = new();
    [JsonPropertyName("plot")]
    public EisPlotData Plot { get; set; } = new();
}

public sealed class EisPlotData
{
    [JsonPropertyName("freq_hz")]
    public List<double> Freq_Hz { get; set; } = new();
    [JsonPropertyName("zreal_ohm")]
    public List<double> Zreal_Ohm { get; set; } = new();
    [JsonPropertyName("zimag_ohm")]
    public List<double> Zimag_Ohm { get; set; } = new();
    [JsonPropertyName("zreal_fit_ohm")]
    public List<double> Zreal_Fit_Ohm { get; set; } = new();
    [JsonPropertyName("zimag_fit_ohm")]
    public List<double> Zimag_Fit_Ohm { get; set; } = new();
    [JsonPropertyName("zmod_ohm")]
    public List<double> Zmod_Ohm { get; set; } = new();
    [JsonPropertyName("phase_deg")]
    public List<double> Phase_Deg { get; set; } = new();
    [JsonPropertyName("zmod_fit_ohm")]
    public List<double> Zmod_Fit_Ohm { get; set; } = new();
    [JsonPropertyName("phase_fit_deg")]
    public List<double> Phase_Fit_Deg { get; set; } = new();
}
