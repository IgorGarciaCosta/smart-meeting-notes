using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Windows;
using SmartMeetingNotes.Api;

namespace SmartMeetingNotes.Desktop;

public partial class App : Application
{
    private CancellationTokenSource? _cts;

    /// <summary>
    /// The "app root" is the directory containing the exe.
    /// All resources (appsettings.json, wwwroot/, scripts/, venv/, data/) are relative to it.
    /// </summary>
    private static string AppRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _cts = new CancellationTokenSource();

        var appRoot = AppRoot;
        Directory.SetCurrentDirectory(appRoot);
        Debug.WriteLine($"[Desktop] App root: {appRoot}");

        // Ensure data directory exists
        Directory.CreateDirectory(Path.Combine(appRoot, "data", "meetings"));
        Directory.CreateDirectory(Path.Combine(appRoot, "data", "audio"));

        // The content root for ASP.NET (where appsettings.json and wwwroot/ are)
        var contentRoot = appRoot;

        // Start the embedded API on a fixed local port
        const string url = "http://localhost:5117";

        // Run API in background (non-blocking)
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var app = ApiHostBuilder.Create([], contentRoot: contentRoot);
                app.Urls.Add(url);
                token.Register(() => app.StopAsync());
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Desktop] API crashed: {ex}");
                Dispatcher.Invoke(() =>
                    MessageBox.Show($"API failed to start:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error));
            }
        });

        // Wait for the API to be ready (poll health endpoint)
        var ready = await WaitForApiReady(url, token);

        if (!ready)
        {
            MessageBox.Show("API did not start in time. Check console output.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Open the main window
        var window = new MainWindow(url);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cts?.Cancel();
        base.OnExit(e);
    }

    private static async Task<bool> WaitForApiReady(string url, CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var response = await http.GetAsync($"{url}/health", ct);
                if (response.IsSuccessStatusCode) return true;
            }
            catch { /* server not ready yet */ }
            await Task.Delay(300, ct);
        }
        return false;
    }
}
