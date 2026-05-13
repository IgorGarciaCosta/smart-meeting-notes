using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public interface IWhisperService
{
    Task<TranscriptionResult> TranscribeAsync(string audioFilePath);
}
