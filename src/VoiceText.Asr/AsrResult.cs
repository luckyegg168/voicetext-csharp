// src/VoiceText.Asr/AsrResult.cs
namespace VoiceText.Asr;

public record AsrResult(string Text, string Language, double DurationMs);
