// src/VoiceText.Tests/Llm/PolishServiceTests.cs
using FluentAssertions;
using Moq;
using VoiceText.Config;
using VoiceText.Llm;

namespace VoiceText.Tests.Llm;

public class PolishServiceTests
{
    [Fact]
    public async Task PolishAsync_calls_llm_with_correct_system_prompt()
    {
        var mockLlm = new Mock<ILlmService>();
        mockLlm.Setup(x => x.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
               .ReturnsAsync("Polished result.");

        var settings = AppSettings.Default with { PolishPromptStyle = "natural", LlmModelName = "llama3.2" };
        var svc = new PolishService(mockLlm.Object, () => settings);

        var result = await svc.PolishAsync("raw text");

        result.Should().Be("Polished result.");
        mockLlm.Verify(x => x.CompleteAsync("llama3.2",
            PromptTemplates.PolishSystemNatural, "raw text", It.IsAny<CancellationToken>()), Times.Once);
    }
}
