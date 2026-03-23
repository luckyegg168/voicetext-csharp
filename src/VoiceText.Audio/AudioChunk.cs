// src/VoiceText.Audio/AudioChunk.cs
namespace VoiceText.Audio;

public record AudioChunk(float[] Samples, int SampleRate, DateTime CapturedAt)
{
    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples.Length / SampleRate);
}
