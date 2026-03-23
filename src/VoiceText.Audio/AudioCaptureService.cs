// src/VoiceText.Audio/AudioCaptureService.cs
using NAudio.Wave;

namespace VoiceText.Audio;

public class AudioCaptureService : IAudioCaptureService
{
    private WaveInEvent? _waveIn;
    private readonly object _lock = new();
    private const int ChunkMs = 30;   // VAD chunk size: 30ms

    public event EventHandler<AudioChunk>? ChunkAvailable;

    public IReadOnlyList<(string Id, string Name)> GetAvailableDevices()
    {
        var devices = new List<(string, string)>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var caps = WaveInEvent.GetCapabilities(i);
            devices.Add((i.ToString(), caps.ProductName));
        }
        return devices;
    }

    public void StartCapture(string? deviceId = null)
    {
        StopCapture();
        _waveIn = new WaveInEvent
        {
            DeviceNumber = int.TryParse(deviceId, out int id) ? id : 0,
            WaveFormat = new WaveFormat(44100, 16, 1),
            BufferMilliseconds = ChunkMs,
        };
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.StartRecording();
    }

    public void StopCapture()
    {
        _waveIn?.StopRecording();
        _waveIn?.Dispose();
        _waveIn = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Convert Int16 PCM to float
        var floats = new float[e.BytesRecorded / 2];
        for (int i = 0; i < floats.Length; i++)
            floats[i] = BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;

        // Resample 44100 → 16000
        var resampled = AudioResampler.Resample(floats, 44100, 16000);
        ChunkAvailable?.Invoke(this, new AudioChunk(resampled, 16000, DateTime.UtcNow));
    }

    public void Dispose() => StopCapture();
}
