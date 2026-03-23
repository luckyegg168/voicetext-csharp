// src/VoiceText.Asr/QwenAsrHttpService.cs
using System.Net.Http.Headers;
using System.Text.Json;

namespace VoiceText.Asr;

public class QwenAsrHttpService : IAsrService
{
    private readonly HttpClient _http;

    public QwenAsrHttpService(HttpClient http) { _http = http; }

    public async Task<AsrResult> TranscribeAsync(float[] audio16kHz, string? language, CancellationToken ct = default)
    {
        var wavBytes = ToWav(audio16kHz, 16000);
        using var form = new MultipartFormDataContent();
        var audioContent = new ByteArrayContent(wavBytes);
        audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(audioContent, "audio", "audio.wav");
        if (!string.IsNullOrEmpty(language))
            form.Add(new StringContent(language), "language");

        var response = await _http.PostAsync("/transcribe", form, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"ASR server error: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json).RootElement;
        return new AsrResult(
            doc.GetProperty("text").GetString()!,
            doc.GetProperty("language").GetString()!,
            doc.GetProperty("duration_ms").GetDouble());
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.GetAsync("/health", ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static byte[] ToWav(float[] samples, int sampleRate)
    {
        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);
        int byteCount = samples.Length * 2;
        writer.Write("RIFF"u8.ToArray()); writer.Write(36 + byteCount);
        writer.Write("WAVE"u8.ToArray()); writer.Write("fmt "u8.ToArray());
        writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(sampleRate); writer.Write(sampleRate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write(byteCount);
        foreach (var s in samples)
            writer.Write((short)(s * 32767f));
        return ms.ToArray();
    }
}
