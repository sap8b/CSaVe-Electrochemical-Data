using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CSaVe_Electrochemical_Data.Models;

namespace CSaVe_Electrochemical_Data.Services;

/// <summary>
/// Invokes the Python/scipy EIS analysis subprocess.
/// </summary>
/// <remarks>
/// Polarization-curve analysis has been removed from the Python side and is now
/// handled entirely by the native C# <see cref="CSaVe_Electrochemical_Data.PolarizationAnalysisService"/>,
/// which is fully debuggable in Visual Studio and follows SOLID design principles.
/// This class is retained only for EIS analysis via Python/scipy.
/// </remarks>
public sealed class PythonAnalysisService
{
    private readonly string _pythonExecutable;
    private readonly string _analysisRunnerPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PythonAnalysisService(string repoRootPath, string pythonExecutable = null)
    {
        _pythonExecutable = string.IsNullOrWhiteSpace(pythonExecutable)
            ? Environment.GetEnvironmentVariable("CSAVE_PYTHON_EXECUTABLE") ?? "python"
            : pythonExecutable;

        _analysisRunnerPath = Path.Combine(repoRootPath, "Python", "analysis", "run_analysis.py");
        if (!File.Exists(_analysisRunnerPath))
            throw new FileNotFoundException($"Python analysis runner not found at {_analysisRunnerPath}");
    }

    /// <summary>
    /// Run EIS analysis by calling the Python/scipy subprocess.
    /// </summary>
    /// <param name="request">EIS analysis request parameters.</param>
    /// <returns>EIS analysis response with fitted circuit parameters and plot data.</returns>
    public EisAnalysisResponse RunEis(EisAnalysisRequest request)
        => Run<EisAnalysisResponse>("eis", request);

    private T Run<T>(string mode, object request)
        where T : AnalysisResponseBase, new()
    {
        string inputPath = Path.GetTempFileName();
        string outputPath = Path.GetTempFileName();

        try
        {
            File.WriteAllText(inputPath, JsonSerializer.Serialize(request));

            var psi = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                ArgumentList =
                {
                    _analysisRunnerPath,
                    mode,
                    inputPath,
                    outputPath
                },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_analysisRunnerPath) ?? Environment.CurrentDirectory
            };

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start Python process.");
            Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stdErrTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(stdOutTask, stdErrTask);
            string stdOut = stdOutTask.Result;
            string stdErr = stdErrTask.Result;

            if (!File.Exists(outputPath))
                throw new InvalidOperationException($"Python analysis did not produce an output file. ExitCode={process.ExitCode}. stderr={stdErr}");

            string json = File.ReadAllText(outputPath);
            T result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Failed to deserialize Python analysis response.");

            if (!result.Success)
            {
                string details = string.IsNullOrWhiteSpace(result.Message) ? stdErr : result.Message;
                if (!string.IsNullOrWhiteSpace(stdOut))
                    details = $"{details} stdout={stdOut}";
                throw new InvalidOperationException($"Python analysis failed: {details}");
            }

            return result;
        }
        catch (Exception ex)
        {
            return new T
            {
                Success = false,
                Message = ex.Message
            };
        }
        finally
        {
            TryDelete(inputPath);
            TryDelete(outputPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best effort cleanup only.
        }
    }
}
