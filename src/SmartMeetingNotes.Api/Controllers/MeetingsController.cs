using Microsoft.AspNetCore.Mvc;
using SmartMeetingNotes.Api.Models;
using SmartMeetingNotes.Api.Services;

namespace SmartMeetingNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingStore _store;
    private readonly MeetingProcessingQueue _queue;
    private readonly ChunkProcessingQueue _chunkQueue;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeetingsController> _logger;

    public MeetingsController(
        IMeetingStore store,
        MeetingProcessingQueue queue,
        ChunkProcessingQueue chunkQueue,
        IConfiguration configuration,
        ILogger<MeetingsController> logger)
    {
        _store = store;
        _queue = queue;
        _chunkQueue = chunkQueue;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get a meeting by ID (includes status, transcript, and analysis when ready).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Meeting), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var meeting = await _store.GetAsync(id);
        if (meeting == null)
            return NotFound(new { error = $"Meeting {id} not found" });

        return Ok(meeting);
    }

    /// <summary>
    /// List all meetings with their current status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Meeting>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var meetings = await _store.GetAllAsync();
        return Ok(meetings);
    }

    // ─── Chunked Upload Endpoints ───────────────────────────────────────────────

    /// <summary>
    /// Create a new meeting for chunked upload. Returns the meeting ID.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MeetingUploadResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateMeetingRequest request)
    {
        var meeting = new Meeting
        {
            Title = request.Title ?? "Untitled Meeting",
            Status = MeetingStatus.AwaitingChunks,
        };

        await _store.SaveAsync(meeting);
        _logger.LogInformation("Meeting {MeetingId} created for chunked upload", meeting.Id);

        return CreatedAtAction(nameof(GetById), new { id = meeting.Id }, new MeetingUploadResponse
        {
            MeetingId = meeting.Id,
            Status = meeting.Status,
            Message = "Meeting created. Upload audio chunks to proceed.",
        });
    }

    /// <summary>
    /// Upload an audio chunk for a meeting. Each chunk is transcribed independently.
    /// </summary>
    [HttpPost("{id:guid}/chunks")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadChunk(Guid id, IFormFile audio, [FromForm] int chunkIndex)
    {
        var meeting = await _store.GetAsync(id);
        if (meeting == null)
            return NotFound(new { error = $"Meeting {id} not found" });

        if (meeting.Status != MeetingStatus.AwaitingChunks)
            return BadRequest(new { error = $"Meeting is in status '{meeting.Status}', cannot upload chunks" });

        if (audio == null || audio.Length == 0)
            return BadRequest(new { error = "No audio file provided" });

        var maxChunkSize = _configuration.GetValue<long>("Upload:MaxChunkSizeBytes", 50 * 1024 * 1024);
        if (audio.Length > maxChunkSize)
            return BadRequest(new { error = $"Chunk exceeds maximum size of {maxChunkSize / (1024 * 1024)} MB" });

        var maxChunks = _configuration.GetValue<int>("Upload:MaxChunksPerMeeting", 60);
        if (meeting.Chunks.Count >= maxChunks)
            return BadRequest(new { error = $"Maximum number of chunks ({maxChunks}) reached" });

        if (meeting.Chunks.Any(c => c.ChunkIndex == chunkIndex))
            return Conflict(new { error = $"Chunk {chunkIndex} already uploaded" });

        var allowedExtensions = new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".webm" };
        var extension = Path.GetExtension(audio.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = $"Unsupported file type: {extension}" });

        var audioDir = _configuration.GetValue<string>("DataPaths:Audio") ?? "data/audio";
        Directory.CreateDirectory(audioDir);

        var fileName = $"{id}_chunk_{chunkIndex}{extension}";
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

        // Enqueue chunk for transcription
        await _chunkQueue.EnqueueAsync(id, chunkIndex);
        _logger.LogInformation("Meeting {MeetingId} chunk {ChunkIndex} uploaded and enqueued", id, chunkIndex);

        return Accepted(new { meetingId = id, chunkIndex, status = chunk.Status });
    }

    /// <summary>
    /// Get the status of all chunks for a meeting.
    /// </summary>
    [HttpGet("{id:guid}/chunks")]
    [ProducesResponseType(typeof(List<AudioChunk>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChunks(Guid id)
    {
        var meeting = await _store.GetAsync(id);
        if (meeting == null)
            return NotFound(new { error = $"Meeting {id} not found" });

        return Ok(meeting.Chunks.OrderBy(c => c.ChunkIndex));
    }

    /// <summary>
    /// Finalize a chunked meeting — merges transcriptions and triggers analysis.
    /// All chunks must be transcribed before calling this.
    /// </summary>
    [HttpPost("{id:guid}/finalize")]
    [ProducesResponseType(typeof(MeetingUploadResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Finalize(Guid id)
    {
        var meeting = await _store.GetAsync(id);
        if (meeting == null)
            return NotFound(new { error = $"Meeting {id} not found" });

        if (meeting.Status != MeetingStatus.AwaitingChunks)
            return BadRequest(new { error = $"Meeting is in status '{meeting.Status}', cannot finalize" });

        if (meeting.Chunks.Count == 0)
            return BadRequest(new { error = "No chunks uploaded" });

        var pendingChunks = meeting.Chunks.Where(c => c.Status != ChunkStatus.Transcribed).ToList();
        if (pendingChunks.Count > 0)
        {
            var statuses = pendingChunks.Select(c => $"chunk {c.ChunkIndex}: {c.Status}");
            return BadRequest(new { error = "Not all chunks are transcribed", pending = statuses });
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

        // Enqueue for Gemini analysis
        await _queue.EnqueueAsync(meeting.Id);
        _logger.LogInformation("Meeting {MeetingId} finalized and enqueued for analysis", meeting.Id);

        return Accepted(new MeetingUploadResponse
        {
            MeetingId = meeting.Id,
            Status = meeting.Status,
            Message = "Meeting finalized. Analysis will start shortly.",
        });
    }
}
