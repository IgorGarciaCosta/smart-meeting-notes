using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public interface IGeminiService
{
    Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript);
}
