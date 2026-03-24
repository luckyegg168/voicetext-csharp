// src/VoiceText.Audio/VadPipeline.cs
namespace VoiceText.Audio;

public class VadPipeline : IDisposable
{
    private readonly VadEngine _vad;
    private double _silenceTimeoutMs;
    private double _minVolumePercent;
    private const int VadChunkSamples = 512; // 32ms at 16kHz
    private const int SampleRate = 16000;
    private const int SpeechPadMs = 300;
    private double _minSpeechDurationMs = 250;

    public bool IsAvailable => _vad.IsAvailable;
    public float EngineThreshold
    {
        get => _vad.Threshold;
        set => _vad.Threshold = value;
    }
    public double MinSpeechDurationMs
    {
        get => _minSpeechDurationMs;
        set => _minSpeechDurationMs = Math.Max(20, value);
    }
    public double SilenceTimeoutMs
    {
        get => _silenceTimeoutMs;
        set => _silenceTimeoutMs = value;
    }
    public double MinVolumePercent
    {
        get => _minVolumePercent;
        set => _minVolumePercent = Math.Max(0, value);
    }

    public VadPipeline(VadEngine vad, double silenceTimeoutMs = 1500)
    {
        _vad = vad;
        _silenceTimeoutMs = silenceTimeoutMs;
    }

    public VadAnalysisResult ExtractSpeech(float[] samples)
    {
        if (!_vad.IsAvailable || samples.Length == 0)
            return new VadAnalysisResult(
                samples.Length == 0 ? null : samples,
                0,
                0,
                0,
                0);

        _vad.Reset();

        var minSpeechSamples = MsToSamples(_minSpeechDurationMs);
        var minSilenceSamples = MsToSamples(_silenceTimeoutMs);
        var speechPadSamples = MsToSamples(SpeechPadMs);

        var segments = new List<(int Start, int End)>();
        bool triggered = false;
        int speechStart = 0;
        int silenceSamples = 0;
        int index = 0;
        double maxVolumePercent = 0;
        float maxSpeechProbability = 0;
        int speechWindowCount = 0;
        int windowCount = 0;

        while (index + VadChunkSamples <= samples.Length)
        {
            var window = new float[VadChunkSamples];
            Array.Copy(samples, index, window, 0, VadChunkSamples);

            var volumePercent = CalculateRms(window) * 100.0;
            maxVolumePercent = Math.Max(maxVolumePercent, volumePercent);
            var speechProbability = _vad.GetSpeechProbability(window, SampleRate);
            maxSpeechProbability = Math.Max(maxSpeechProbability, speechProbability);
            bool isSpeech = volumePercent >= _minVolumePercent && speechProbability >= _vad.Threshold;
            windowCount++;

            if (isSpeech)
            {
                speechWindowCount++;
                silenceSamples = 0;
                if (!triggered)
                {
                    triggered = true;
                    speechStart = Math.Max(0, index - speechPadSamples);
                }
            }
            else if (triggered)
            {
                silenceSamples += VadChunkSamples;
                if (silenceSamples >= minSilenceSamples)
                {
                    var speechEnd = Math.Min(samples.Length, index - silenceSamples + VadChunkSamples + speechPadSamples);
                    if (speechEnd - speechStart >= minSpeechSamples)
                        segments.Add((speechStart, speechEnd));
                    triggered = false;
                    silenceSamples = 0;
                }
            }

            index += VadChunkSamples;
        }

        if (triggered)
        {
            var speechEnd = samples.Length;
            if (speechEnd - speechStart >= minSpeechSamples)
                segments.Add((speechStart, speechEnd));
        }

        if (segments.Count == 0)
            return new VadAnalysisResult(null, maxVolumePercent, maxSpeechProbability, speechWindowCount, windowCount);

        var merged = new List<float>();
        foreach (var (start, end) in segments)
        {
            for (int i = start; i < end && i < samples.Length; i++)
                merged.Add(samples[i]);
        }

        var filtered = merged.Count == 0 ? null : merged.ToArray();
        return new VadAnalysisResult(
            filtered,
            maxVolumePercent,
            maxSpeechProbability,
            speechWindowCount,
            windowCount);
    }

    /// <summary>Resets all internal VAD state for a fresh recording session.</summary>
    public void ResetState()
    {
        _vad.Reset();
    }

    public void Dispose() => _vad.Dispose();

    private static int MsToSamples(double milliseconds) =>
        Math.Max(VadChunkSamples, (int)(SampleRate * (milliseconds / 1000.0)));

    private static double CalculateRms(float[] samples)
    {
        if (samples.Length == 0) return 0;
        double sum = 0;
        foreach (var sample in samples)
            sum += sample * sample;
        return Math.Sqrt(sum / samples.Length);
    }
}
