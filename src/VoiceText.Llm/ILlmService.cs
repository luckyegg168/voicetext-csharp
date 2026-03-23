// src/VoiceText.Llm/ILlmService.cs
namespace VoiceText.Llm;

public interface ILlmService
{
    Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
                               CancellationToken ct = default);
    IAsyncEnumerable<string> StreamAsync(string model, string systemPrompt, string userPrompt,
                                          CancellationToken ct = default);
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}
