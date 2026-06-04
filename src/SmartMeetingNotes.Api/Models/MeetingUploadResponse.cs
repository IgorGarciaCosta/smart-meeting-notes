namespace SmartMeetingNotes.Api.Models;

public class MeetingUploadResponse
{
    public Guid MeetingId { get; set; }
    public MeetingStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}
