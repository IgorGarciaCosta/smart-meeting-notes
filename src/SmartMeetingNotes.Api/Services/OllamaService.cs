using System.Text;
using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls a local Ollama instance to analyze meeting transcripts.
/// Extracts: summary, action items, decisions, pending questions.
/// </summary>
public class OllamaService : IAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaService> _logger;
    private readonly string _model;

    private const string AnalysisPrompt = """
        You are a meeting analysis assistant. Analyze the following meeting transcript and extract:

        1. **Summary**: A concise technical summary of the meeting (2-4 paragraphs).
        2. **Action Items**: A list of tasks/actions with responsible people if mentioned.
        3. **Decisions**: Key decisions that were made during the meeting.
        4. **Pending Questions**: Unresolved questions or topics that need follow-up.

        Respond ONLY with valid JSON in this exact format (no markdown, no extra text):
        {
          "summary": "...",
          "actionItems": ["..."],
          "decisions": ["..."],
          "pendingQuestions": ["..."]
        }

        If the transcript is in Portuguese, respond in Portuguese.

        TRANSCRIPT:
        """;

    public OllamaService(HttpClient httpClient, ILogger<OllamaService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _model = configuration["Ollama:Model"] ?? "qwen2.5:14b";
    }

    public async Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript)
    {
        _logger.LogInformation("Sending transcript to Ollama ({Model}) for analysis ({Length} chars)", _model, transcript.Length);

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "user", content = AnalysisPrompt + transcript }
            },
            stream = false,
            format = "json",
            options = new
            {
                temperature = 0.3,
                num_ctx = 8192
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/v1/chat/completions", content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Ollama response received ({Length} chars)", jsonResponse.Length);

        using var doc = JsonDocument.Parse(jsonResponse);
        var textResult = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()
            ?? throw new InvalidOperationException("Empty response from Ollama");

        var analysis = JsonSerializer.Deserialize<MeetingAnalysis>(textResult, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Failed to deserialize Ollama analysis");

        return analysis;
    }
}
