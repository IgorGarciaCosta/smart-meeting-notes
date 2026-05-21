using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public interface IAnalysisService
{
    Task<MeetingAnalysis> AnalyzeTranscriptAsync(string transcript);
}
