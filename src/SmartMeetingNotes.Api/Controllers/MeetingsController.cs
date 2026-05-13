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
    private readonly IConfiguration _configuration;
    private readonly ILogger<MeetingsController> _logger;

    public MeetingsController(
        IMeetingStore store,
        MeetingProcessingQueue queue,
        IConfiguration configuration,
        ILogger<MeetingsController> logger)
    {
        _store = store;
        _queue = queue;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Upload an audio file to create a new meeting.
    /// Returns 202 Accepted with the meeting ID — processing happens in background.
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(MeetingUploadResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile audio, [FromForm] string title = "Untitled Meeting")
    {
        if (audio == null || audio.Length == 0)
            return BadRequest(new { error = "No audio file provided" });

        var allowedExtensions = new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".webm" };
        var extension = Path.GetExtension(audio.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = $"Unsupported file type: {extension}" });

        // Save audio to data/audio/
        var audioDir = _configuration.GetValue<string>("DataPaths:Audio") ?? "data/audio";
        Directory.CreateDirectory(audioDir);

        var meeting = new Meeting
        {
            Title = title,
            Status = MeetingStatus.Uploaded,
        };

        var fileName = $"{meeting.Id}{extension}";
        var filePath = Path.Combine(audioDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await audio.CopyToAsync(stream);
        }

        meeting.AudioFilePath = filePath;
        await _store.SaveAsync(meeting);

        // Enqueue for background processing
        await _queue.EnqueueAsync(meeting.Id);
        _logger.LogInformation("Meeting {MeetingId} uploaded and enqueued for processing", meeting.Id);

        return Accepted(new MeetingUploadResponse
        {
            MeetingId = meeting.Id,
            Status = meeting.Status,
            Message = "Meeting uploaded. Processing will start shortly.",
        });
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
}
