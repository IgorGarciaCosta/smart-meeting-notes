namespace SmartMeetingNotes.Api.Models;

public class TranscriptionSegment
{
    public double Start { get; set; }
    public double End { get; set; }
    public string Text { get; set; } = string.Empty;
}

public class TranscriptionResult
{
    public string Text { get; set; } = string.Empty;
    public List<TranscriptionSegment> Segments { get; set; } = [];
    public string Language { get; set; } = string.Empty;
    public double LanguageProbability { get; set; }
    public double DurationSeconds { get; set; }
}
