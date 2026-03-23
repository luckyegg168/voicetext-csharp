// src/VoiceText.Tests/Llm/OllamaServiceTests.cs
using FluentAssertions;
using System.Net;
using VoiceText.Llm;

namespace VoiceText.Tests.Llm;

public class OllamaServiceTests
{
    [Fact]
    public async Task Complete_returns_response_text()
    {
        var json = """{"model":"llama3.2","message":{"role":"assistant","content":"Polished text."}}""";
        var svc = new OllamaService(MakeFakeClient(json));
        var result = await svc.CompleteAsync("llama3.2", "system", "Polish this.");
        result.Should().Be("Polished text.");
    }

    private static HttpClient MakeFakeClient(string json) =>
        new(new OllamaTestHandler(json)) { BaseAddress = new Uri("http://localhost:11434") };
}

class OllamaTestHandler(string response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(response)
        });
}
