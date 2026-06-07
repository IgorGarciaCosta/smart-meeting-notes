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

        var (pythonPath, scriptsRoot) = ResolvePythonPaths(configuration);
        _runner = new PythonProcessRunner(pythonPath, scriptsRoot, logger);
    }

    internal static (string pythonPath, string scriptsRoot) ResolvePythonPaths(IConfiguration configuration)
    {
        var baseDir = Directory.GetCurrentDirectory();

        // Scripts root: where transcriber/, analyzer/, check_models.py live
        var scriptsRoot = configuration.GetValue<string>("Whisper:ProjectRoot") ?? "";
        if (string.IsNullOrEmpty(scriptsRoot) || !Directory.Exists(scriptsRoot))
        {
            // Published layout: scripts/ subfolder next to exe
            var candidate = Path.Combine(baseDir, "scripts");
            if (Directory.Exists(candidate))
                scriptsRoot = candidate;
            else
                scriptsRoot = baseDir; // dev: CWD is repo root
        }

        // Python path
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath") ?? "";
        if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
        {
            if (OperatingSystem.IsWindows())
            {
                // Try venv next to exe (published) or in CWD (dev)
                var venvPython = Path.Combine(baseDir, "venv", "Scripts", "python.exe");
                pythonPath = File.Exists(venvPython) ? venvPython : "python";
            }
            else
            {
                pythonPath = "python3";
            }
        }

        return (pythonPath, scriptsRoot);
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
