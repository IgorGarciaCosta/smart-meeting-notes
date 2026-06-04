using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartMeetingNotes.Api.Models;
using SmartMeetingNotes.Api.Services;

namespace SmartMeetingNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly PythonProcessRunner _runner;
    private readonly RuntimeSettings _settings;

    public ModelsController(ILogger<ModelsController> logger, IConfiguration configuration, RuntimeSettings settings)
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

    /// <summary>Check availability of currently configured models.</summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var arguments = $"check_models.py \"{_settings.WhisperModel}\" \"{_settings.AnalyzerModelRepo}\" \"{_settings.AnalyzerModelFile}\"";
            var stdout = await _runner.RunAsync(arguments, "ModelCheck", cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(stdout);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new[]
            {
                new { name = "Whisper", model = _settings.WhisperModel, available = false, reason = $"Erro ao verificar: {ex.Message}" },
                new { name = "Qwen", model = _settings.AnalyzerModelFile, available = false, reason = $"Erro ao verificar: {ex.Message}" },
            });
        }
    }

    /// <summary>Get current runtime model settings.</summary>
    [HttpGet("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            whisperModel = _settings.WhisperModel,
            whisperDevice = _settings.WhisperDevice,
            analyzerProvider = _settings.AnalyzerProvider,
            analyzerEndpoint = _settings.AnalyzerEndpoint,
            analyzerModelRepo = _settings.AnalyzerModelRepo,
            analyzerModelFile = _settings.AnalyzerModelFile,
            analyzerLocalModelPath = _settings.AnalyzerLocalModelPath,
            ollamaModel = _settings.OllamaModel,
        });
    }

    /// <summary>Update runtime model settings (takes effect immediately for next processing).</summary>
    [HttpPut("settings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult UpdateSettings([FromBody] UpdateModelSettingsRequest request)
    {
        if (request.WhisperModel != null) _settings.WhisperModel = request.WhisperModel;
        if (request.WhisperDevice != null) _settings.WhisperDevice = request.WhisperDevice;
        if (request.AnalyzerProvider != null) _settings.AnalyzerProvider = request.AnalyzerProvider;
        if (request.AnalyzerEndpoint != null) _settings.AnalyzerEndpoint = request.AnalyzerEndpoint;
        if (request.AnalyzerModelRepo != null) _settings.AnalyzerModelRepo = request.AnalyzerModelRepo;
        if (request.AnalyzerModelFile != null) _settings.AnalyzerModelFile = request.AnalyzerModelFile;
        if (request.AnalyzerLocalModelPath != null) _settings.AnalyzerLocalModelPath = request.AnalyzerLocalModelPath;
        if (request.OllamaModel != null) _settings.OllamaModel = request.OllamaModel;

        _settings.Save();

        return Ok(new
        {
            whisperModel = _settings.WhisperModel,
            whisperDevice = _settings.WhisperDevice,
            analyzerProvider = _settings.AnalyzerProvider,
            analyzerEndpoint = _settings.AnalyzerEndpoint,
            analyzerModelRepo = _settings.AnalyzerModelRepo,
            analyzerModelFile = _settings.AnalyzerModelFile,
            analyzerLocalModelPath = _settings.AnalyzerLocalModelPath,
            ollamaModel = _settings.OllamaModel,
        });
    }

    /// <summary>List available Whisper model options.</summary>
    [HttpGet("whisper/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetWhisperModels()
    {
        var models = new[]
        {
            new { id = "tiny", name = "Tiny", quality = "Low", speed = "Very fast", notes = "Quick testing only" },
            new { id = "base", name = "Base", quality = "Fair", speed = "Fast", notes = "Basic accuracy" },
            new { id = "small", name = "Small", quality = "Good", speed = "Fast", notes = "Limited hardware" },
            new { id = "medium", name = "Medium", quality = "Very Good", speed = "Medium", notes = "Best balance for CPU" },
            new { id = "large-v3", name = "Large V3", quality = "Best", speed = "Slow", notes = "Best quality (GPU recommended)" },
            new { id = "distil-large-v3", name = "Distil Large V3", quality = "Great", speed = "Fast", notes = "Near large-v3 quality, much faster" },
        };
        return Ok(models);
    }

    /// <summary>Scan local machine for available GGUF models (HuggingFace cache + custom paths).</summary>
    [HttpGet("analyzer/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAnalyzerModels(CancellationToken cancellationToken)
    {
        try
        {
            var stdout = await _runner.RunAsync("scan_local_models.py", "ModelScan", cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(stdout);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new { models = Array.Empty<object>(), error = ex.Message });
        }
    }

    /// <summary>List models available from Ollama (if running).</summary>
    [HttpGet("ollama/available")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOllamaModels(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync("http://localhost:11434/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Ok(new { available = false, models = Array.Empty<object>() });

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(json);
            return Ok(new { available = true, models = result });
        }
        catch
        {
            return Ok(new { available = false, models = Array.Empty<object>(), reason = "Ollama not running on localhost:11434" });
        }
    }
}
