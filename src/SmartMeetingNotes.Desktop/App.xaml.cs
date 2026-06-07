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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _cts = new CancellationTokenSource();

        // Resolve the API project content root (where appsettings.json and wwwroot live)
        var apiContentRoot = FindApiContentRoot();
        Debug.WriteLine($"[Desktop] API content root: {apiContentRoot}");

        // Set the current directory to the API root so relative paths (data/) work
        Directory.SetCurrentDirectory(apiContentRoot);

        // Start the embedded API on a fixed local port
        const string url = "http://localhost:5117";

        // Run API in background (non-blocking)
        var token = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                var app = ApiHostBuilder.Create([], contentRoot: apiContentRoot);
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

    private static string FindApiContentRoot()
    {
        // Walk up from the executable to find the API project folder
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "src", "SmartMeetingNotes.Api");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent == dir) break;
            dir = parent;
        }
        // Fallback: use CWD-based path
        var cwd = Directory.GetCurrentDirectory();
        var fallback = Path.Combine(cwd, "src", "SmartMeetingNotes.Api");
        if (Directory.Exists(fallback))
            return fallback;
        return cwd;
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
