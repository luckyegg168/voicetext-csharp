// src/VoiceText.Llm/PolishService.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class PolishService
{
    private readonly ILlmService _llm;
    private readonly Func<AppSettings> _getSettings;

    public PolishService(ILlmService llm, Func<AppSettings> getSettings)
    {
        _llm = llm;
        _getSettings = getSettings;
    }

    public Task<string> PolishAsync(string rawText, CancellationToken ct = default)
    {
        var s = _getSettings();
        var sys = PromptTemplates.GetPolishSystem(s.PolishPromptStyle);
        return _llm.CompleteAsync(s.LlmModelName, sys, rawText, ct);
    }

    public IAsyncEnumerable<string> PolishStreamAsync(string rawText, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.StreamAsync(s.LlmModelName, PromptTemplates.GetPolishSystem(s.PolishPromptStyle), rawText, ct);
    }
}
