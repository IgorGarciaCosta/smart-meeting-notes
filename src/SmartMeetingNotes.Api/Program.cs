using SmartMeetingNotes.Api.Middleware;
using SmartMeetingNotes.Api.Services;

// Load .env file if it exists (GEMINI_API_KEY etc.)
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel max request body size (200 MB default)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

// Add .env variables to configuration
builder.Configuration.AddEnvironmentVariables();

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Smart Meeting Notes API", Version = "v1" });
});

// Health checks
builder.Services.AddHealthChecks();

// CORS (permissive for MVP)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// Meeting store (JSON files)
builder.Services.AddSingleton<IMeetingStore, JsonMeetingStore>();

// Processing queue (in-memory channel)
builder.Services.AddSingleton<MeetingProcessingQueue>();

// Chunk processing queue and service
builder.Services.AddSingleton<ChunkProcessingQueue>();

// Whisper transcription via Python subprocess (no separate server needed)
builder.Services.AddSingleton<IWhisperService, WhisperService>();

// Ollama (local LLM) HTTP client
builder.Services.AddHttpClient<IAnalysisService, OllamaService>(client =>
{
    var ollamaUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

// Background processing services
builder.Services.AddHostedService<MeetingProcessingService>();
builder.Services.AddHostedService<ChunkProcessingService>();

var app = builder.Build();

// --- Middleware pipeline ---
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Smart Meeting Notes API v1");
    });
}

app.UseCors();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
