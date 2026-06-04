using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SmartMeetingNotes.Api.Services;

namespace SmartMeetingNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ModelsController : ControllerBase
{
    private readonly PythonProcessRunner _runner;
    private readonly string _whisperModel;
    private readonly string _qwenRepo;
    private readonly string _qwenFile;

    public ModelsController(ILogger<ModelsController> logger, IConfiguration configuration)
    {
        var projectRoot = configuration.GetValue<string>("Whisper:ProjectRoot")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var pythonPath = configuration.GetValue<string>("Whisper:PythonPath")
            ?? Path.Combine(projectRoot, "venv", "Scripts", "python.exe");

        _whisperModel = configuration.GetValue<string>("Whisper:Model") ?? "large-v3";
        _qwenRepo = configuration.GetValue<string>("Analyzer:ModelRepo") ?? "Qwen/Qwen2.5-7B-Instruct-GGUF";
        _qwenFile = configuration.GetValue<string>("Analyzer:ModelFile") ?? "qwen2.5-7b-instruct-q3_k_m.gguf";

        _runner = new PythonProcessRunner(pythonPath, projectRoot, logger);
    }

    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var arguments = $"check_models.py \"{_whisperModel}\" \"{_qwenRepo}\" \"{_qwenFile}\"";
            var stdout = await _runner.RunAsync(arguments, "ModelCheck", cancellationToken);
            var result = JsonSerializer.Deserialize<JsonElement>(stdout);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Ok(new[]
            {
                new { name = "Whisper", model = _whisperModel, available = false, reason = $"Erro ao verificar: {ex.Message}" },
                new { name = "Qwen", model = _qwenFile, available = false, reason = $"Erro ao verificar: {ex.Message}" },
            });
        }
    }
}
