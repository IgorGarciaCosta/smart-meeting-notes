using System.Text;
using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Calls Gemini 2.5 Flash API to analyze meeting transcripts.
/// Extracts: summary, action items, decisions, pending questions.
/// </summary>
public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiService> _logger;
    private readonly string _apiKey;

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

    public GeminiService(HttpClient httpClient, ILogger<GeminiService> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["GEMINI_API_KEY"] ?? string.Empty;
    }

    public async Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("GEMINI_API_KEY not configured. Create a .env file with GEMINI_API_KEY=your-key");

        _logger.LogInformation("Sending transcript to Gemini for analysis ({Length} chars)", transcript.Length);

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = AnalysisPrompt + transcript }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                responseMimeType = "application/json",
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var url = $"/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Gemini response received ({Length} chars)", jsonResponse.Length);

        // Gemini response structure: { candidates: [{ content: { parts: [{ text: "..." }] } }] }
        using var doc = JsonDocument.Parse(jsonResponse);
        var textResult = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString()
            ?? throw new InvalidOperationException("Empty response from Gemini");

        var analysis = JsonSerializer.Deserialize<MeetingAnalysis>(textResult, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Failed to deserialize Gemini analysis");

        return analysis;
    }
}
