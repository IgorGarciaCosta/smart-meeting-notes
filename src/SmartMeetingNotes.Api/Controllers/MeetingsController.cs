using Microsoft.AspNetCore.Mvc;
using SmartMeetingNotes.Api.Models;
using SmartMeetingNotes.Api.Services;

namespace SmartMeetingNotes.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeetingsController : ControllerBase
{
    private readonly IMeetingService _meetingService;

    public MeetingsController(IMeetingService meetingService)
    {
        _meetingService = meetingService;
    }

    /// <summary>
    /// Get a meeting by ID (includes status, transcript, and analysis when ready).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(Meeting), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var meeting = await _meetingService.GetMeetingAsync(id);
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
        var meetings = await _meetingService.GetAllMeetingsAsync();
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
        var meeting = await _meetingService.CreateMeetingAsync(request);

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
        var result = await _meetingService.UploadChunkAsync(id, audio, chunkIndex);

        return result.StatusCode switch
        {
            StatusCodes.Status202Accepted => Accepted(result.Response),
            StatusCodes.Status404NotFound => NotFound(result.Response),
            StatusCodes.Status409Conflict => Conflict(result.Response),
            _ => BadRequest(result.Response),
        };
    }

    /// <summary>
    /// Get the status of all chunks for a meeting.
    /// </summary>
    [HttpGet("{id:guid}/chunks")]
    [ProducesResponseType(typeof(List<AudioChunk>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChunks(Guid id)
    {
        var chunks = await _meetingService.GetChunksAsync(id);
        if (chunks == null)
            return NotFound(new { error = $"Meeting {id} not found" });

        return Ok(chunks);
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
        var result = await _meetingService.FinalizeMeetingAsync(id);

        return result.StatusCode switch
        {
            StatusCodes.Status202Accepted => Accepted(result.Response),
            StatusCodes.Status404NotFound => NotFound(result.Response),
            _ => BadRequest(result.Response),
        };
    }

    /// <summary>
    /// Delete a meeting and its associated audio files.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _meetingService.DeleteMeetingAsync(id);
        if (!deleted)
            return NotFound(new { error = $"Meeting {id} not found" });

        return NoContent();
    }
}
