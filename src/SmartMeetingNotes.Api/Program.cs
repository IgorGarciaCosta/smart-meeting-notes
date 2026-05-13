using SmartMeetingNotes.Api.Middleware;
using SmartMeetingNotes.Api.Services;

// Load .env file if it exists (GEMINI_API_KEY etc.)
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

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

// Whisper transcription via Python subprocess (no separate server needed)
builder.Services.AddSingleton<IWhisperService, WhisperService>();

// Gemini HTTP client → Google Generative AI API
builder.Services.AddHttpClient<IGeminiService, GeminiService>(client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Background processing service
builder.Services.AddHostedService<MeetingProcessingService>();

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
