namespace SmartMeetingNotes.Api.Models;

public class UpdateModelSettingsRequest
{
    public string? WhisperModel { get; set; }
    public string? WhisperDevice { get; set; }
    public string? AnalyzerProvider { get; set; }
    public string? AnalyzerEndpoint { get; set; }
    public string? AnalyzerModelRepo { get; set; }
    public string? AnalyzerModelFile { get; set; }
    public string? AnalyzerLocalModelPath { get; set; }
    public string? OllamaModel { get; set; }
}
