namespace VoiceText.Audio;

public sealed record VadAnalysisResult(
    float[]? FilteredSamples,
    double MaxVolumePercent,
    float MaxSpeechProbability,
    int SpeechWindowCount,
    int WindowCount);
