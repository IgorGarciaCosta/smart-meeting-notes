using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Whisper Python transcriber via subprocess.
/// Reads model/device from RuntimeSettings so the user can switch models at runtime.
/// </summary>
public class WhisperService : IWhisperService
{
    private readonly PythonProcessRunner _runner;
    private readonly RuntimeSettings _settings;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WhisperService(ILogger<WhisperService> logger, IConfiguration configuration, RuntimeSettings settings)
    {
        _settings = settings;

        var projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? (OperatingSystem.IsWindows()
                ? Path.Combine(projectRoot, "venv", "Scripts", "python.exe")
                : "python3");

        _runner = new PythonProcessRunner(pythonPath, projectRoot, logger);
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        var absoluteAudioPath = Path.GetFullPath(audioFilePath);
        var arguments = $"-m transcriber.transcribe --json \"{absoluteAudioPath}\" {_settings.WhisperModel} {_settings.WhisperDevice}";

        var stdout = await _runner.RunAsync(arguments, "Whisper", cancellationToken);

        var result = JsonSerializer.Deserialize<TranscriptionResult>(stdout, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Whisper output");

        return result;
    }
}
