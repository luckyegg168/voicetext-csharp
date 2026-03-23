// src/VoiceText.Asr/AsrServerManager.cs
using System.Diagnostics;

namespace VoiceText.Asr;

public class AsrServerManager : IDisposable
{
    private Process? _process;
    private readonly string _pythonExe;
    // workingDir is the PARENT of asr_server/ so that
    // "python -m asr_server.main" resolves the package correctly.
    private readonly string _workingDir;
    private readonly int _port;

    public AsrServerManager(string pythonExe, string workingDir, int port = 8765)
    {
        _pythonExe = pythonExe;
        _workingDir = workingDir;
        _port = port;
    }

    public void Start()
    {
        if (_process is { HasExited: false }) return;

        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonExe,
                Arguments = "-m asr_server.main",
                WorkingDirectory = _workingDir,   // parent dir, NOT asr_server/ itself
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        _process.StartInfo.EnvironmentVariables["ASR_PORT"] = _port.ToString();
        _process.Start();
    }

    /// <summary>
    /// Polls GET /health until the server responds 2xx or <paramref name="timeoutMs"/> elapses.
    /// Returns true if the server became ready in time, false on timeout.
    /// </summary>
    public async Task<bool> WaitForReadyAsync(int timeoutMs = 30_000, CancellationToken ct = default)
    {
        using var http = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{_port}"),
            Timeout = TimeSpan.FromSeconds(2),
        };

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            try
            {
                var resp = await http.GetAsync("/health", ct);
                if (resp.IsSuccessStatusCode) return true;
            }
            catch { /* server not up yet */ }

            await Task.Delay(500, ct);
        }
        return false;
    }

    public void Stop()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(3000);
        }
        _process?.Dispose();
        _process = null;
    }

    public bool IsRunning => _process is { HasExited: false };

    public void Dispose() => Stop();
}
