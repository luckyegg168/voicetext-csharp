// src/VoiceText.Llm/LlamaCppService.cs
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace VoiceText.Llm;

public class LlamaCppService : ILlmService
{
    private readonly HttpClient _http;
    public LlamaCppService(HttpClient http) { _http = http; }

    public async Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
                                             CancellationToken ct = default)
    {
        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            stream = false,
        };
        var resp = await _http.PostAsJsonAsync("/v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();
        var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString()!;
    }

    public async IAsyncEnumerable<string> StreamAsync(string model, string systemPrompt, string userPrompt,
                                                       [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = new
        {
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            stream = true,
        };
        var resp = await _http.PostAsJsonAsync("/v1/chat/completions", payload, ct);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            var data = line["data: ".Length..];
            if (data == "[DONE]") break;
            var doc = JsonDocument.Parse(data);
            var delta = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("delta")
                .GetProperty("content")
                .GetString();
            if (!string.IsNullOrEmpty(delta)) yield return delta;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try { return (await _http.GetAsync("/health", ct)).IsSuccessStatusCode; }
        catch { return false; }
    }
}
