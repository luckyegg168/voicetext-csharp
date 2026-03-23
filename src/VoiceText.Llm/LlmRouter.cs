// src/VoiceText.Llm/LlmRouter.cs
using VoiceText.Config;

namespace VoiceText.Llm;

public class LlmRouter : ILlmService
{
    private readonly Func<AppSettings> _getSettings;
    private readonly ILlmService _ollama;
    private readonly ILlmService _llamaCpp;

    public LlmRouter(Func<AppSettings> getSettings, ILlmService ollama, ILlmService llamaCpp)
    {
        _getSettings = getSettings;
        _ollama = ollama;
        _llamaCpp = llamaCpp;
    }

    private ILlmService Active => _getSettings().LlmBackend == "LlamaCpp" ? _llamaCpp : _ollama;

    public Task<string> CompleteAsync(string model, string sys, string user, CancellationToken ct = default)
        => Active.CompleteAsync(model, sys, user, ct);
    public IAsyncEnumerable<string> StreamAsync(string model, string sys, string user, CancellationToken ct = default)
        => Active.StreamAsync(model, sys, user, ct);
    public Task<bool> IsHealthyAsync(CancellationToken ct = default) => Active.IsHealthyAsync(ct);
}
