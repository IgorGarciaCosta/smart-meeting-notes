namespace SmartMeetingNotes.Api.Models;

public class Meeting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public MeetingStatus Status { get; set; } = MeetingStatus.AwaitingChunks;
    public List<AudioChunk> Chunks { get; set; } = [];
    public TranscriptionResult? Transcript { get; set; }
    public MeetingAnalysis? Analysis { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsChunked => Chunks.Count > 0;
}
