using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

public interface IMeetingStore
{
    Task SaveAsync(Meeting meeting);
    Task<Meeting?> GetAsync(Guid id);
    Task<List<Meeting>> GetAllAsync();
}
