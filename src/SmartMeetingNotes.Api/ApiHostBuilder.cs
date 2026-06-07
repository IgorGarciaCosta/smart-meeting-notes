using SmartMeetingNotes.Api.Middleware;
using SmartMeetingNotes.Api.Services;

namespace SmartMeetingNotes.Api;

/// <summary>
/// Builds and configures the Web API host. Used both by the standalone API
/// and by the Desktop app to embed the server.
/// </summary>
public static class ApiHostBuilder
{
    public static WebApplication Create(string[] args, string? contentRoot = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = contentRoot,
        });

        // Load .env file if it exists
        DotNetEnv.Env.Load();

        // Configure Kestrel max request body size (200 MB default)
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 200 * 1024 * 1024;
        });

        // Add .env variables to configuration
        builder.Configuration.AddEnvironmentVariables();

        // --- Services ---
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ApiHostBuilder).Assembly)
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "Smart Meeting Notes API", Version = "v1" });
        });

        // Health checks
        builder.Services.AddHealthChecks();

        // CORS (permissive for local use)
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        // Meeting store (JSON files)
        builder.Services.AddSingleton<IMeetingStore, JsonMeetingStore>();

        // Runtime settings
        var dataDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "data");
        var settingsPath = Path.Combine(Path.GetFullPath(dataDir), "settings.json");
        var runtimeSettings = RuntimeSettings.Load(settingsPath, builder.Configuration);
        builder.Services.AddSingleton(runtimeSettings);

        // Meeting service
        builder.Services.AddScoped<IMeetingService, MeetingService>();

        // Processing queues
        builder.Services.AddSingleton<MeetingProcessingQueue>();
        builder.Services.AddSingleton<ChunkProcessingQueue>();

        // Whisper transcription via Python subprocess
        builder.Services.AddSingleton<IWhisperService, WhisperService>();

        // Qwen analysis via Python subprocess
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

        // Serve embedded frontend (wwwroot)
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapHealthChecks("/health");
        app.MapControllers();

        // Fallback: serve index.html for SPA routes
        app.MapFallbackToFile("index.html");

        return app;
    }
}
