using System.Collections.Generic;
using System.Text.Json.Serialization;

// NOTE: Polarization analysis JSON contracts (PolarizationAnalysisRequest,
// PolarizationAnalysisResponse, PolarizationFileResult, PolarizationPlotData)
// have been removed from this file.  Polarization analysis is now handled entirely
// by the native C# pipeline; the relevant contracts live in
// Services/Polarization/Models/.
namespace CSaVe_Electrochemical_Data.Models;

public sealed class EisAnalysisRequest
{
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
    [JsonPropertyName("model")]
    public string Model { get; set; } = "randles_cpe_w";
}

/// <summary>Base class for all analysis responses returned by the Python subprocess.</summary>
public class AnalysisResponseBase
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
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
