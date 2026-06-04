namespace SmartMeetingNotes.Api.Models;

public class MeetingAnalysis
{
    public string Summary { get; set; } = string.Empty;
    public List<string> ActionItems { get; set; } = [];
    public List<string> Decisions { get; set; } = [];
    public List<string> PendingQuestions { get; set; } = [];
}
