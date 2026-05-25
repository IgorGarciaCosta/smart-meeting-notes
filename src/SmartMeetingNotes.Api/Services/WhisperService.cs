using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Whisper Python transcriber via subprocess.
/// Runs: python -m transcriber.transcribe --json <audioFile>
/// </summary>
public class WhisperService : IWhisperService
{
    private readonly PythonProcessRunner _runner;
    private readonly string _whisperModel;
    private readonly string _whisperDevice;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WhisperService(ILogger<WhisperService> logger, IConfiguration configuration)
    {
        var projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? Path.Combine(projectRoot, "venv", "Scripts", "python.exe");
        _whisperModel = configuration.GetValue<string>("Whisper:Model") ?? "distil-large-v3";
        _whisperDevice = configuration.GetValue<string>("Whisper:Device") ?? "cpu";

        _runner = new PythonProcessRunner(pythonPath, projectRoot, logger);
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath, CancellationToken cancellationToken = default)
    {
        var absoluteAudioPath = Path.GetFullPath(audioFilePath);
        var arguments = $"-m transcriber.transcribe --json \"{absoluteAudioPath}\" {_whisperModel} {_whisperDevice}";

        var stdout = await _runner.RunAsync(arguments, "Whisper", cancellationToken);

        var result = JsonSerializer.Deserialize<TranscriptionResult>(stdout, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Whisper output");

        return result;
    }
}
