// src/VoiceText.Llm/TranslationService.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class TranslationService
{
    private readonly ILlmService _llm;
    private readonly Func<AppSettings> _getSettings;

    public TranslationService(ILlmService llm, Func<AppSettings> getSettings)
    {
        _llm = llm;
        _getSettings = getSettings;
    }

    public Task<string> TranslateToEnglishAsync(string text, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.CompleteAsync(s.LlmModelName, PromptTemplates.TranslationSystemToEnglish, text, ct);
    }

    public Task<string> TranslateToChineseAsync(string text, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.CompleteAsync(s.LlmModelName, PromptTemplates.TranslationSystemToChinese, text, ct);
    }
}
