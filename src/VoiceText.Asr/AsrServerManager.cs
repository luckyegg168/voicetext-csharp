// src/VoiceText.Asr/AsrServerManager.cs
using System.Diagnostics;

namespace VoiceText.Asr;

public class AsrServerManager : IDisposable
{
    private Process? _process;
    private readonly string _pythonExe;
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
                WorkingDirectory = _workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            },
        };
        _process.StartInfo.EnvironmentVariables["ASR_PORT"] = _port.ToString();
        _process.Start();
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
