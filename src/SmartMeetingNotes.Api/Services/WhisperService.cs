using System.Diagnostics;
using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Whisper Python transcriber via subprocess.
/// Runs: python -m transcriber.transcribe --json <audioFile>
/// No need for a separate FastAPI server.
/// </summary>
public class WhisperService : IWhisperService
{
    private readonly ILogger<WhisperService> _logger;
    private readonly string _pythonPath;
    private readonly string _projectRoot;
    private readonly string _whisperModel;
    private readonly string _whisperDevice;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WhisperService(ILogger<WhisperService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        _pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? Path.Combine(_projectRoot, "venv", "Scripts", "python.exe");
        _whisperModel = configuration.GetValue<string>("Whisper:Model") ?? "medium";
        _whisperDevice = configuration.GetValue<string>("Whisper:Device") ?? "cpu";
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath)
    {
        var absoluteAudioPath = Path.GetFullPath(audioFilePath);
        _logger.LogInformation("Transcribing via subprocess: {File} (model={Model}, device={Device})",
            Path.GetFileName(absoluteAudioPath), _whisperModel, _whisperDevice);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = $"-m transcriber.transcribe --json \"{absoluteAudioPath}\" {_whisperModel} {_whisperDevice}",
            WorkingDirectory = _projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start Python process");

        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
            _logger.LogError("Whisper process failed (exit {Code}): {Detail}", process.ExitCode, errorDetail);
            throw new InvalidOperationException($"Whisper transcription failed: {errorDetail}");
        }

        _logger.LogInformation("Whisper process finished ({Length} chars output)", stdout.Length);

        var result = JsonSerializer.Deserialize<TranscriptionResult>(stdout, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Whisper output");

        return result;
    }
}
