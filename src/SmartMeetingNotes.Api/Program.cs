using SmartMeetingNotes.Api.Middleware;
using SmartMeetingNotes.Api.Services;

// Load .env file if it exists
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Use PORT env var (Render sets this)
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://+:{port}");

// Configure Kestrel max request body size (200 MB default)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
});

// Add .env variables to configuration
builder.Configuration.AddEnvironmentVariables();

// --- Services ---
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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

// Runtime settings (mutable, user can change models from the UI)
var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data");
var settingsPath = Path.Combine(Path.GetFullPath(dataDir), "settings.json");
var runtimeSettings = RuntimeSettings.Load(settingsPath, builder.Configuration);
builder.Services.AddSingleton(runtimeSettings);

// Meeting service (application layer)
builder.Services.AddScoped<IMeetingService, MeetingService>();

// Processing queue (in-memory channel)
builder.Services.AddSingleton<MeetingProcessingQueue>();

// Chunk processing queue and service
builder.Services.AddSingleton<ChunkProcessingQueue>();

// Whisper transcription via Python subprocess (no separate server needed)
builder.Services.AddSingleton<IWhisperService, WhisperService>();

// Qwen analysis via Python subprocess (no separate server needed)
builder.Services.AddSingleton<IAnalysisService, QwenAnalysisService>();

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
