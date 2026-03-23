// src/VoiceText.Tests/Audio/VadEngineTests.cs
using FluentAssertions;
using VoiceText.Audio;

namespace VoiceText.Tests.Audio;

public class VadEngineTests
{
    [Fact]
    public void Silence_chunk_returns_false()
    {
        var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "silero_vad.onnx");
        if (!File.Exists(modelPath)) return; // Skip if model not downloaded

        using var vad = new VadEngine(modelPath);
        var silence = new float[512]; // all zeros = silence
        vad.IsSpeech(silence, 16000).Should().BeFalse();
    }
}
