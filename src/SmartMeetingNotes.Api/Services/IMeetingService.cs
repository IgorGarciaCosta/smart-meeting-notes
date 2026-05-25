using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public record ChunkUploadResult(bool Success, int StatusCode, object Response);
public record FinalizeResult(bool Success, int StatusCode, object Response);

public interface IMeetingService
{
    Task<Meeting?> GetMeetingAsync(Guid id);
    Task<List<Meeting>> GetAllMeetingsAsync();
    Task<Meeting> CreateMeetingAsync(CreateMeetingRequest request);
    Task<ChunkUploadResult> UploadChunkAsync(Guid meetingId, IFormFile audio, int chunkIndex);
    Task<List<AudioChunk>> GetChunksAsync(Guid meetingId);
    Task<FinalizeResult> FinalizeMeetingAsync(Guid meetingId);
}
