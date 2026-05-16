using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Background service that transcribes individual audio chunks as they arrive.
/// </summary>
public class ChunkProcessingService : BackgroundService
{
    private readonly ChunkProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ChunkProcessingService> _logger;

    public ChunkProcessingService(
        ChunkProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ChunkProcessingService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chunk processing service started");

        await foreach (var (meetingId, chunkIndex) in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessChunkAsync(meetingId, chunkIndex, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing chunk {ChunkIndex} of meeting {MeetingId}",
                    chunkIndex, meetingId);
            }
        }
    }

    private async Task ProcessChunkAsync(Guid meetingId, int chunkIndex, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMeetingStore>();
        var whisper = scope.ServiceProvider.GetRequiredService<IWhisperService>();

        var meeting = await store.GetAsync(meetingId);
        if (meeting == null)
        {
            _logger.LogWarning("Meeting {MeetingId} not found, skipping chunk {ChunkIndex}", meetingId, chunkIndex);
            return;
        }

        var chunk = meeting.Chunks.FirstOrDefault(c => c.ChunkIndex == chunkIndex);
        if (chunk == null)
        {
            _logger.LogWarning("Chunk {ChunkIndex} not found in meeting {MeetingId}", chunkIndex, meetingId);
            return;
        }

        _logger.LogInformation("[{MeetingId}] Transcribing chunk {ChunkIndex}...", meetingId, chunkIndex);

        try
        {
            chunk.Status = ChunkStatus.Transcribing;
            await store.SaveAsync(meeting);

            var transcription = await whisper.TranscribeAsync(chunk.FilePath);
            chunk.Transcript = transcription;
            chunk.Status = ChunkStatus.Transcribed;
            await store.SaveAsync(meeting);

            _logger.LogInformation("[{MeetingId}] Chunk {ChunkIndex} transcribed ({Chars} chars)",
                meetingId, chunkIndex, transcription.Text.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{MeetingId}] Chunk {ChunkIndex} transcription failed", meetingId, chunkIndex);
            chunk.Status = ChunkStatus.Failed;
            chunk.ErrorMessage = ex.Message;
            await store.SaveAsync(meeting);
        }
    }
}
