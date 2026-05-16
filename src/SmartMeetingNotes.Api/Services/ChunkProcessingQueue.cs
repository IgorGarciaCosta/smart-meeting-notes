using System.Threading.Channels;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// In-memory queue for chunk transcription jobs.
/// MeetingsController writes (meetingId, chunkIndex) here; ChunkProcessingService reads them.
/// </summary>
public class ChunkProcessingQueue
{
    private readonly Channel<(Guid MeetingId, int ChunkIndex)> _channel =
        Channel.CreateUnbounded<(Guid, int)>();

    public async ValueTask EnqueueAsync(Guid meetingId, int chunkIndex)
    {
        await _channel.Writer.WriteAsync((meetingId, chunkIndex));
    }

    public IAsyncEnumerable<(Guid MeetingId, int ChunkIndex)> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
