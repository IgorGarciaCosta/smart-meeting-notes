namespace SmartMeetingNotes.Api.Models;

public enum ChunkStatus
{
    Uploaded,
    Transcribing,
    Transcribed,
    Failed
}

public class AudioChunk
{
    public int ChunkIndex { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public ChunkStatus Status { get; set; } = ChunkStatus.Uploaded;
    public TranscriptionResult? Transcript { get; set; }
    public string? ErrorMessage { get; set; }
}
