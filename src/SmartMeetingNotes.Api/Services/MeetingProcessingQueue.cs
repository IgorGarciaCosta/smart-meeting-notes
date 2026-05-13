using System.Threading.Channels;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// In-memory queue backed by Channel&lt;Guid&gt;.
/// MeetingsController writes meeting IDs here; MeetingProcessingService reads them.
/// </summary>
public class MeetingProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public async ValueTask EnqueueAsync(Guid meetingId)
    {
        await _channel.Writer.WriteAsync(meetingId);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
