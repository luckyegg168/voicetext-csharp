// src/VoiceText.Tests/Audio/AudioResamplerTests.cs
using FluentAssertions;
using VoiceText.Audio;

namespace VoiceText.Tests.Audio;

public class AudioResamplerTests
{
    [Fact]
    public void Resample_44100_to_16000_preserves_duration()
    {
        var original = new float[44100]; // 1 second at 44.1kHz
        var result = AudioResampler.Resample(original, 44100, 16000);
        result.Length.Should().Be(16000);
    }

    [Fact]
    public void Resample_same_rate_returns_same_length()
    {
        var original = new float[16000];
        var result = AudioResampler.Resample(original, 16000, 16000);
        result.Length.Should().Be(16000);
    }

    [Fact]
    public void StereoToMono_averages_channels()
    {
        // Channel L = 1.0, Channel R = 0.0, expected mono = 0.5
        var stereo = new float[] { 1.0f, 0.0f, 1.0f, 0.0f };
        var mono = AudioResampler.StereoToMono(stereo);
        foreach (var sample in mono)
            sample.Should().BeApproximately(0.5f, 0.001f);
    }
}
