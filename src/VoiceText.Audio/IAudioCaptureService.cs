// src/VoiceText.Audio/IAudioCaptureService.cs
namespace VoiceText.Audio;

public interface IAudioCaptureService : IDisposable
{
    event EventHandler<AudioChunk> ChunkAvailable;
    IReadOnlyList<(string Id, string Name)> GetAvailableDevices();
    void StartCapture(string? deviceId = null);
    void StopCapture();
}
