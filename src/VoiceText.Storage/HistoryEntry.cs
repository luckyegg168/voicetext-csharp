// src/VoiceText.Storage/HistoryEntry.cs
namespace VoiceText.Storage;

public record HistoryEntry(
    int Id,
    DateTime CreatedAt,
    string RawText,
    string? PolishedText,
    string? TranslatedText,
    string Language,
    double DurationMs
);
