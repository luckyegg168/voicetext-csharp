// src/VoiceText.Asr/IAsrService.cs
namespace VoiceText.Asr;

public interface IAsrService
{
    Task<AsrResult> TranscribeAsync(float[] audio16kHz, string? language, CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
