using System.Diagnostics;
using System.Text;

namespace SmartMeetingNotes.Api.Services;

public sealed class PythonProcessRunner
{
    private readonly string _pythonPath;
    private readonly string _workingDirectory;
    private readonly ILogger _logger;

    public PythonProcessRunner(string pythonPath, string workingDirectory, ILogger logger)
    {
        _pythonPath = pythonPath;
        _workingDirectory = workingDirectory;
        _logger = logger;
    }

    public async Task<string> RunAsync(string arguments, string processName, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting {Process}: python {Arguments}", processName, arguments);

        var psi = new ProcessStartInfo
        {
            FileName = _pythonPath,
            Arguments = arguments,
            WorkingDirectory = _workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        psi.Environment["PYTHONUTF8"] = "1";

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {processName} process");

        using var registration = cancellationToken.Register(() =>
        {
            try { process.Kill(entireProcessTree: true); }
            catch { /* process may have already exited */ }
        });

        var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorDetail = !string.IsNullOrWhiteSpace(stderr) ? stderr : stdout;
            _logger.LogError("{Process} failed (exit {Code}): {Detail}", processName, process.ExitCode, errorDetail);
            throw new InvalidOperationException($"{processName} failed: {errorDetail}");
        }

        _logger.LogInformation("{Process} finished ({Length} chars output)", processName, stdout.Length);
        return stdout;
    }
}
