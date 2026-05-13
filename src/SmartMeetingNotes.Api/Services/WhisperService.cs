using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls the Whisper FastAPI service (Python) to transcribe audio files.
/// Expected endpoint: POST http://localhost:8001/transcribe
/// </summary>
public class WhisperService : IWhisperService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhisperService> _logger;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public WhisperService(HttpClient httpClient, ILogger<WhisperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TranscriptionResult> TranscribeAsync(string audioFilePath)
    {
        _logger.LogInformation("Sending audio to Whisper service: {File}", Path.GetFileName(audioFilePath));

        using var form = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(audioFilePath);
        var fileContent = new ByteArrayContent(fileBytes);
        form.Add(fileContent, "file", Path.GetFileName(audioFilePath));

        var response = await _httpClient.PostAsync("/transcribe", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Whisper response received ({Length} chars)", json.Length);

        var result = JsonSerializer.Deserialize<TranscriptionResult>(json, _jsonOptions)
            ?? throw new InvalidOperationException("Failed to deserialize Whisper response");

        return result;
    }
}
