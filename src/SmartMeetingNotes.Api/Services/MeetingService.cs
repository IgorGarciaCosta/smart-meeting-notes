using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public class MeetingService : IMeetingService
{
    private readonly IMeetingStore _store;
    private readonly MeetingProcessingQueue _queue;
    private readonly ChunkProcessingQueue _chunkQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeetingService> _logger;

    private static readonly string[] AllowedExtensions = [".mp3", ".wav", ".m4a", ".ogg", ".flac", ".webm"];

    public MeetingService(
        IMeetingStore store,
        MeetingProcessingQueue queue,
        ChunkProcessingQueue chunkQueue,
        IConfiguration configuration,
        ILogger<MeetingService> logger)
    {
        _store = store;
        _queue = queue;
        _chunkQueue = chunkQueue;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<Meeting?> GetMeetingAsync(Guid id) => _store.GetAsync(id);

    public Task<List<Meeting>> GetAllMeetingsAsync() => _store.GetAllAsync();

    public async Task<Meeting> CreateMeetingAsync(CreateMeetingRequest request)
    {
        var meeting = new Meeting
        {
            Title = request.Title ?? "Untitled Meeting",
            Status = MeetingStatus.AwaitingChunks,
        };

        await _store.SaveAsync(meeting);
        _logger.LogInformation("Meeting {MeetingId} created for chunked upload", meeting.Id);

        return meeting;
    }

    public async Task<ChunkUploadResult> UploadChunkAsync(Guid meetingId, IFormFile audio, int chunkIndex)
    {
        var meeting = await _store.GetAsync(meetingId);
        if (meeting == null)
            return new ChunkUploadResult(false, StatusCodes.Status404NotFound,
                new { error = $"Meeting {meetingId} not found" });

        if (meeting.Status != MeetingStatus.AwaitingChunks)
            return new ChunkUploadResult(false, StatusCodes.Status400BadRequest,
                new { error = $"Meeting is in status '{meeting.Status}', cannot upload chunks" });

        if (audio == null || audio.Length == 0)
            return new ChunkUploadResult(false, StatusCodes.Status400BadRequest,
                new { error = "No audio file provided" });

        var maxChunkSize = _configuration.GetValue<long>("Upload:MaxChunkSizeBytes", 50 * 1024 * 1024);
        if (audio.Length > maxChunkSize)
            return new ChunkUploadResult(false, StatusCodes.Status400BadRequest,
                new { error = $"Chunk exceeds maximum size of {maxChunkSize / (1024 * 1024)} MB" });

        var maxChunks = _configuration.GetValue<int>("Upload:MaxChunksPerMeeting", 200);
        if (meeting.Chunks.Count >= maxChunks)
            return new ChunkUploadResult(false, StatusCodes.Status400BadRequest,
                new { error = $"Maximum number of chunks ({maxChunks}) reached" });

        if (meeting.Chunks.Any(c => c.ChunkIndex == chunkIndex))
            return new ChunkUploadResult(false, StatusCodes.Status409Conflict,
                new { error = $"Chunk {chunkIndex} already uploaded" });

        var extension = Path.GetExtension(audio.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return new ChunkUploadResult(false, StatusCodes.Status400BadRequest,
                new { error = $"Unsupported file type: {extension}" });

        var audioDir = _configuration.GetValue<string>("DataPaths:Audio") ?? "data/audio";
        Directory.CreateDirectory(audioDir);

        var fileName = $"{meetingId}_chunk_{chunkIndex}{extension}";
        var filePath = Path.Combine(audioDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await audio.CopyToAsync(stream);
        }

        var chunk = new AudioChunk
        {
            ChunkIndex = chunkIndex,
            FilePath = filePath,
            Status = ChunkStatus.Uploaded,
        };

        meeting.Chunks.Add(chunk);
        meeting.Chunks = meeting.Chunks.OrderBy(c => c.ChunkIndex).ToList();
        await _store.SaveAsync(meeting);

        await _chunkQueue.EnqueueAsync(meetingId, chunkIndex);
        _logger.LogInformation("Meeting {MeetingId} chunk {ChunkIndex} uploaded and enqueued", meetingId, chunkIndex);

        return new ChunkUploadResult(true, StatusCodes.Status202Accepted,
            new { meetingId, chunkIndex, status = chunk.Status });
    }

    public async Task<List<AudioChunk>> GetChunksAsync(Guid meetingId)
    {
        var meeting = await _store.GetAsync(meetingId);
        if (meeting == null)
            return null!;

        return meeting.Chunks.OrderBy(c => c.ChunkIndex).ToList();
    }

    public async Task<FinalizeResult> FinalizeMeetingAsync(Guid meetingId)
    {
        var meeting = await _store.GetAsync(meetingId);
        if (meeting == null)
            return new FinalizeResult(false, StatusCodes.Status404NotFound,
                new { error = $"Meeting {meetingId} not found" });

        if (meeting.Status != MeetingStatus.AwaitingChunks)
            return new FinalizeResult(false, StatusCodes.Status400BadRequest,
                new { error = $"Meeting is in status '{meeting.Status}', cannot finalize" });

        if (meeting.Chunks.Count == 0)
            return new FinalizeResult(false, StatusCodes.Status400BadRequest,
                new { error = "No chunks uploaded" });

        var pendingChunks = meeting.Chunks.Where(c => c.Status != ChunkStatus.Transcribed).ToList();
        if (pendingChunks.Count > 0)
        {
            var failedChunks = pendingChunks.Where(c => c.Status == ChunkStatus.Failed).ToList();
            if (failedChunks.Count > 0)
            {
                var errors = failedChunks.Select(c => $"chunk {c.ChunkIndex}: {c.ErrorMessage}");
                return new FinalizeResult(false, StatusCodes.Status400BadRequest,
                    new { error = "Some chunks failed transcription", failed = errors });
            }

            var statuses = pendingChunks.Select(c => $"chunk {c.ChunkIndex}: {c.Status}");
            return new FinalizeResult(false, StatusCodes.Status202Accepted,
                new { status = "pending", message = "Not all chunks are transcribed yet", pending = statuses });
        }

        // Merge transcriptions in order
        var orderedChunks = meeting.Chunks.OrderBy(c => c.ChunkIndex).ToList();
        var mergedText = string.Join(" ", orderedChunks.Select(c => c.Transcript!.Text));
        var mergedSegments = new List<TranscriptionSegment>();
        double timeOffset = 0;

        foreach (var chunk in orderedChunks)
        {
            foreach (var segment in chunk.Transcript!.Segments)
            {
                mergedSegments.Add(new TranscriptionSegment
                {
                    Start = segment.Start + timeOffset,
                    End = segment.End + timeOffset,
                    Text = segment.Text,
                });
            }
            timeOffset += chunk.Transcript.DurationSeconds;
        }

        meeting.Transcript = new TranscriptionResult
        {
            Text = mergedText,
            Segments = mergedSegments,
            Language = orderedChunks.First().Transcript!.Language,
            LanguageProbability = orderedChunks.First().Transcript!.LanguageProbability,
            DurationSeconds = timeOffset,
        };

        meeting.Status = MeetingStatus.Finalizing;
        await _store.SaveAsync(meeting);

        await _queue.EnqueueAsync(meeting.Id);
        _logger.LogInformation("Meeting {MeetingId} finalized and enqueued for analysis", meeting.Id);

        return new FinalizeResult(true, StatusCodes.Status202Accepted, new MeetingUploadResponse
        {
            MeetingId = meeting.Id,
            Status = meeting.Status,
            Message = "Meeting finalized. Analysis will start shortly.",
        });
    }
}
