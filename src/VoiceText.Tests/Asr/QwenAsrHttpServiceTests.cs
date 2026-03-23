// src/VoiceText.Tests/Asr/QwenAsrHttpServiceTests.cs
using FluentAssertions;
using System.Net;
using VoiceText.Asr;

namespace VoiceText.Tests.Asr;

public class QwenAsrHttpServiceTests
{
    private static HttpClient MakeFakeClient(string responseJson, HttpStatusCode code = HttpStatusCode.OK)
    {
        var handler = new TestHttpMessageHandler(responseJson, code);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8765") };
    }

    [Fact]
    public async Task Transcribe_returns_text_on_success()
    {
        var json = """{"text":"Hello world","language":"English","duration_ms":120.5}""";
        var svc = new QwenAsrHttpService(MakeFakeClient(json));
        var audio = new float[16000];

        var result = await svc.TranscribeAsync(audio, "English");

        result.Text.Should().Be("Hello world");
        result.Language.Should().Be("English");
    }

    [Fact]
    public async Task Transcribe_throws_on_server_error()
    {
        var svc = new QwenAsrHttpService(MakeFakeClient("error", HttpStatusCode.InternalServerError));
        var act = () => svc.TranscribeAsync(new float[16000], null);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

class TestHttpMessageHandler(string response, HttpStatusCode code) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(response) });
}
