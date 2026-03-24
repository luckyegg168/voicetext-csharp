using System.Text.Json;

namespace VoiceText.Llm;

public class OllamaModelCatalog
{
    private readonly HttpClient _http;

    public OllamaModelCatalog(HttpClient http)
    {
        _http = http;
    }

    public async Task<IReadOnlyList<string>> GetModelNamesAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _http.GetAsync("tags", ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var models = doc.RootElement
                .GetProperty("models")
                .EnumerateArray()
                .Select(x => x.GetProperty("name").GetString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();

            return models;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
