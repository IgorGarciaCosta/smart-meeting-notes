using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Qwen Python analyzer via subprocess.
/// Runs: python -m analyzer.analyze --json <transcriptFile>
/// </summary>
public class QwenAnalysisService : IAnalysisService
{
    private readonly PythonProcessRunner _runner;
    private readonly string _modelRepo;
    private readonly string _modelFile;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public QwenAnalysisService(ILogger<QwenAnalysisService> logger, IConfiguration configuration)
    {
        var projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? Path.Combine(projectRoot, "venv", "Scripts", "python.exe");
        _modelRepo = configuration.GetValue<string>("Analyzer:ModelRepo")
            ?? "Qwen/Qwen2.5-7B-Instruct-GGUF";
        _modelFile = configuration.GetValue<string>("Analyzer:ModelFile")
            ?? "qwen2.5-7b-instruct-q4_k_m.gguf";

        _runner = new PythonProcessRunner(pythonPath, projectRoot, logger);
    }

    public async Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript, CancellationToken cancellationToken = default)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, transcript, System.Text.Encoding.UTF8, cancellationToken);

        try
        {
            var arguments = $"-m analyzer.analyze --json \"{tempFile}\" \"{_modelRepo}\" \"{_modelFile}\"";
            var stdout = await _runner.RunAsync(arguments, "Analyzer", cancellationToken);

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
