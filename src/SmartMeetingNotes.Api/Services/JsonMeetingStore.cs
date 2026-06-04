using System.Text.Json;
using SmartMeetingNotes.Api.Models;

namespace SmartMeetingNotes.Api.Services;

/// <summary>
/// Persists meetings as individual JSON files in data/meetings/.
/// Thread-safe via SemaphoreSlim.
/// </summary>
public class JsonMeetingStore : IMeetingStore
{
    private readonly string _basePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public JsonMeetingStore(IConfiguration configuration)
    {
        _basePath = configuration.GetValue<string>("DataPaths:Meetings") ?? "data/meetings";
        Directory.CreateDirectory(_basePath);
    }

    public async Task SaveAsync(Meeting meeting)
    {
        await _lock.WaitAsync();
        try
        {
            var filePath = Path.Combine(_basePath, $"{meeting.Id}.json");
            var json = JsonSerializer.Serialize(meeting, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Meeting?> GetAsync(Guid id)
    {
        var filePath = Path.Combine(_basePath, $"{id}.json");
        if (!File.Exists(filePath))
            return null;

        await _lock.WaitAsync();
        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<Meeting>(json, _jsonOptions);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<Meeting>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var meetings = new List<Meeting>();
            if (!Directory.Exists(_basePath))
                return meetings;

            foreach (var file in Directory.GetFiles(_basePath, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file);
                var meeting = JsonSerializer.Deserialize<Meeting>(json, _jsonOptions);
                if (meeting != null)
                    meetings.Add(meeting);
            }

            return meetings.OrderByDescending(m => m.UploadedAt).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var filePath = Path.Combine(_basePath, $"{id}.json");
        if (!File.Exists(filePath))
            return false;

        await _lock.WaitAsync();
        try
        {
            File.Delete(filePath);
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }
}
