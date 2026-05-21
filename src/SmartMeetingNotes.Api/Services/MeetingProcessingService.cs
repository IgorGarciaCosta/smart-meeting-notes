using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Background service that processes meetings from the queue.
/// Pipeline: Analyzing → Completed (or Failed).
/// </summary>
public class MeetingProcessingService : BackgroundService
{
    private readonly MeetingProcessingQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MeetingProcessingService> _logger;

    public MeetingProcessingService(
        MeetingProcessingQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<MeetingProcessingService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Meeting processing service started");

        await foreach (var meetingId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessMeetingAsync(meetingId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing meeting {MeetingId}", meetingId);
            }
        }
    }

    private async Task ProcessMeetingAsync(Guid meetingId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMeetingStore>();

        var meeting = await store.GetAsync(meetingId);
        if (meeting == null)
        {
            _logger.LogWarning("Meeting {MeetingId} not found, skipping", meetingId);
            return;
        }

        _logger.LogInformation("Processing meeting {MeetingId} — '{Title}'", meetingId, meeting.Title);

        try
        {
            // Transcription already done per-chunk, proceed to analysis
            meeting.Status = MeetingStatus.Analyzing;
            await store.SaveAsync(meeting);

            // Analyze with LLM
            _logger.LogInformation("[{MeetingId}] Analyzing with Gemini...", meetingId);

            var gemini = scope.ServiceProvider.GetRequiredService<IGeminiService>();
            var analysis = await gemini.AnalyzeTranscriptAsync(meeting.Transcript!.Text);
            meeting.Analysis = analysis;

            // Done
            meeting.Status = MeetingStatus.Completed;
            await store.SaveAsync(meeting);
            _logger.LogInformation("[{MeetingId}] Processing completed!", meetingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{MeetingId}] Processing failed", meetingId);
            meeting.Status = MeetingStatus.Failed;
            meeting.ErrorMessage = ex.Message;
            await store.SaveAsync(meeting);
        }
    }
}
