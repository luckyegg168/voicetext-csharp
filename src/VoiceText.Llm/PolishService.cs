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
        var user = BuildPolishInput(rawText);
        return _llm.CompleteAsync(s.LlmModelName, sys, user, ct);
    }

    public IAsyncEnumerable<string> PolishStreamAsync(string rawText, CancellationToken ct = default)
    {
        var s = _getSettings();
        return _llm.StreamAsync(s.LlmModelName, PromptTemplates.GetPolishSystem(s.PolishPromptStyle), BuildPolishInput(rawText), ct);
    }

    private static string BuildPolishInput(string rawText) =>
        $"請只輸出潤稿後文字。以下是待潤稿的語音轉錄內容：\n<transcript>\n{rawText}\n</transcript>";
}
