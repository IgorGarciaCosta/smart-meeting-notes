using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Python analyzer via subprocess.
/// Supports multiple providers: builtin (llama-cpp-python), ollama, or any OpenAI-compatible endpoint.
/// Reads model config from RuntimeSettings so the user can switch models at runtime.
/// </summary>
public class QwenAnalysisService : IAnalysisService
{
    private readonly PythonProcessRunner _runner;
    private readonly RuntimeSettings _settings;
    private readonly ILogger<QwenAnalysisService> _logger;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public QwenAnalysisService(ILogger<QwenAnalysisService> logger, IConfiguration configuration, RuntimeSettings settings)
    {
        _settings = settings;
        _logger = logger;

        var projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? (OperatingSystem.IsWindows()
                ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."))
                : AppContext.BaseDirectory);
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine(projectRoot, "venv", "Scripts", "python.exe")
                : "python3");

        _runner = new PythonProcessRunner(pythonPath, projectRoot, logger);
    }

    public async Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript, CancellationToken cancellationToken = default)
    {
        return _settings.AnalyzerProvider switch
        {
            "ollama" or "openai-compatible" => await AnalyzeViaOpenAICompatibleAsync(transcript, cancellationToken),
            _ => await AnalyzeViaBuiltinAsync(transcript, cancellationToken),
        };
    }

    private async Task<MeetingAnalysis> AnalyzeViaBuiltinAsync(string transcript, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, transcript, System.Text.Encoding.UTF8, cancellationToken);

        try
        {
            var modelRepo = _settings.AnalyzerModelRepo;
            var modelFile = _settings.AnalyzerModelFile;

            // If user set a local model path, pass it directly
            if (!string.IsNullOrEmpty(_settings.AnalyzerLocalModelPath))
            {
                var arguments = $"-m analyzer.analyze --json \"{tempFile}\" --local \"{_settings.AnalyzerLocalModelPath}\"";
                var stdout = await _runner.RunAsync(arguments, "Analyzer", cancellationToken);
                return JsonSerializer.Deserialize<MeetingAnalysis>(stdout, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize analyzer output");
            }
            else
            {
                var arguments = $"-m analyzer.analyze --json \"{tempFile}\" \"{modelRepo}\" \"{modelFile}\"";
                var stdout = await _runner.RunAsync(arguments, "Analyzer", cancellationToken);
                return JsonSerializer.Deserialize<MeetingAnalysis>(stdout, _jsonOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize analyzer output");
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private async Task<MeetingAnalysis> AnalyzeViaOpenAICompatibleAsync(string transcript, CancellationToken cancellationToken)
    {
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, transcript, System.Text.Encoding.UTF8, cancellationToken);

        try
        {
            var endpoint = _settings.AnalyzerEndpoint.TrimEnd('/');
            var model = _settings.OllamaModel;

            var arguments = $"-m analyzer.analyze --json \"{tempFile}\" --openai-endpoint \"{endpoint}\" --openai-model \"{model}\"";
            var stdout = await _runner.RunAsync(arguments, "Analyzer", cancellationToken);

            return JsonSerializer.Deserialize<MeetingAnalysis>(stdout, _jsonOptions)
                ?? throw new InvalidOperationException("Failed to deserialize analyzer output");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
