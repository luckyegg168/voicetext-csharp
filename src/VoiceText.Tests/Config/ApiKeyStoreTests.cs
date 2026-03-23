// src/VoiceText.Tests/Config/ApiKeyStoreTests.cs
using FluentAssertions;
using VoiceText.Config;

namespace VoiceText.Tests.Config;

public class ApiKeyStoreTests
{
    [Fact]
    public void SetAndGet_roundtrips_api_key()
    {
        var store = new ApiKeyStore("TestProfile");
        store.Set("OllamaApiKey", "my-secret-key");
        store.Get("OllamaApiKey").Should().Be("my-secret-key");
        store.Delete("OllamaApiKey");
    }

    [Fact]
    public void Get_returns_null_for_missing_key()
    {
        var store = new ApiKeyStore("TestProfile");
        store.Get("NonExistentKey").Should().BeNull();
    }
}
