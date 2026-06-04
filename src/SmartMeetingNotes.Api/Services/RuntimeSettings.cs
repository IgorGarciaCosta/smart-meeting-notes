using System.Text.Json;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Mutable runtime settings that can be changed from the frontend.
/// Persisted to a JSON file so changes survive restarts.
/// </summary>
public class RuntimeSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _settingsPath;
    private readonly object _lock = new();

    public string WhisperModel { get; set; } = "large-v3";
    public string WhisperDevice { get; set; } = "cpu";
    public string AnalyzerProvider { get; set; } = "builtin"; // "builtin" | "ollama" | "openai-compatible"
    public string AnalyzerEndpoint { get; set; } = "http://localhost:11434/v1";
    public string AnalyzerModelRepo { get; set; } = "Qwen/Qwen2.5-7B-Instruct-GGUF";
    public string AnalyzerModelFile { get; set; } = "qwen2.5-7b-instruct-q3_k_m.gguf";
    public string AnalyzerLocalModelPath { get; set; } = "";
    public string OllamaModel { get; set; } = "";

    public RuntimeSettings(string settingsPath)
    {
        _settingsPath = settingsPath;
    }

    public void Save()
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
    }

    public static RuntimeSettings Load(string settingsPath, IConfiguration configuration)
    {
        if (File.Exists(settingsPath))
        {
            try
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<RuntimeSettingsDto>(json, JsonOptions);
                if (settings != null)
                {
                    return new RuntimeSettings(settingsPath)
                    {
                        WhisperModel = settings.WhisperModel ?? configuration.GetValue<string>("Whisper:Model") ?? "large-v3",
                        WhisperDevice = settings.WhisperDevice ?? configuration.GetValue<string>("Whisper:Device") ?? "cpu",
                        AnalyzerProvider = settings.AnalyzerProvider ?? "builtin",
                        AnalyzerEndpoint = settings.AnalyzerEndpoint ?? "http://localhost:11434/v1",
                        AnalyzerModelRepo = settings.AnalyzerModelRepo ?? configuration.GetValue<string>("Analyzer:ModelRepo") ?? "Qwen/Qwen2.5-7B-Instruct-GGUF",
                        AnalyzerModelFile = settings.AnalyzerModelFile ?? configuration.GetValue<string>("Analyzer:ModelFile") ?? "qwen2.5-7b-instruct-q3_k_m.gguf",
                        AnalyzerLocalModelPath = settings.AnalyzerLocalModelPath ?? "",
                        OllamaModel = settings.OllamaModel ?? "",
                    };
                }
            }
            catch
            {
                // Fall through to defaults
            }
        }

        // Initialize from appsettings
        return new RuntimeSettings(settingsPath)
        {
            WhisperModel = configuration.GetValue<string>("Whisper:Model") ?? "large-v3",
            WhisperDevice = configuration.GetValue<string>("Whisper:Device") ?? "cpu",
            AnalyzerModelRepo = configuration.GetValue<string>("Analyzer:ModelRepo") ?? "Qwen/Qwen2.5-7B-Instruct-GGUF",
            AnalyzerModelFile = configuration.GetValue<string>("Analyzer:ModelFile") ?? "qwen2.5-7b-instruct-q3_k_m.gguf",
        };
    }

    /// <summary>DTO for deserialization (all nullable to handle partial files).</summary>
    private class RuntimeSettingsDto
    {
        public string? WhisperModel { get; set; }
        public string? WhisperDevice { get; set; }
        public string? AnalyzerProvider { get; set; }
        public string? AnalyzerEndpoint { get; set; }
        public string? AnalyzerModelRepo { get; set; }
        public string? AnalyzerModelFile { get; set; }
        public string? AnalyzerLocalModelPath { get; set; }
        public string? OllamaModel { get; set; }
    }
}
