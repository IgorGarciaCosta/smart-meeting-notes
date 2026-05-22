using System.Diagnostics;
using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Qwen Python analyzer via subprocess.
/// Runs: python -m analyzer.analyze --json <transcriptFile>
/// No need for a separate server (Ollama, etc.).
/// </summary>
public class QwenAnalysisService : IAnalysisService
{
    private readonly ILogger<QwenAnalysisService> _logger;
    private readonly string _pythonPath;
    private readonly string _projectRoot;
    private readonly string _modelRepo;
    private readonly string _modelFile;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public QwenAnalysisService(ILogger<QwenAnalysisService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? Path.Combine(_projectRoot, "venv", "Scripts", "python.exe");
        _modelRepo = configuration.GetValue<string>("Analyzer:ModelRepo")
            ?? "Qwen/Qwen2.5-7B-Instruct-GGUF";
        _modelFile = configuration.GetValue<string>("Analyzer:ModelFile")
            ?? "qwen2.5-7b-instruct-q4_k_m.gguf";
    }

    public async Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript)
    {
        _logger.LogInformation("Analyzing transcript via subprocess ({Length} chars, model={Repo}/{File})",
            transcript.Length, _modelRepo, _modelFile);

        // Write transcript to a temp file to avoid argument length limits
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, transcript, System.Text.Encoding.UTF8);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"-m analyzer.analyze --json \"{tempFile}\" \"{_modelRepo}\" \"{_modelFile}\"",
                WorkingDirectory = _projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Python analyzer process");

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
                _logger.LogError("Analyzer process failed (exit {Code}): {Detail}", process.ExitCode, errorDetail);
                throw new InvalidOperationException($"Analysis failed: {errorDetail}");
            }

            _logger.LogInformation("Analyzer process finished ({Length} chars output)", stdout.Length);

            var result = JsonSerializer.Deserialize<MeetingAnalysis>(stdout, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize analyzer output");

            return result;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
