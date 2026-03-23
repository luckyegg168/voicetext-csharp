// src/VoiceText.Tests/Config/SettingsServiceTests.cs
using FluentAssertions;
using VoiceText.Config;

namespace VoiceText.Tests.Config;

public class SettingsServiceTests
{
    [Fact]
    public void Save_and_Load_roundtrips_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.json");
        var svc = new SettingsService(path);
        var original = AppSettings.Default with { AsrServerPort = 9999 };

        svc.Save(original);
        var loaded = svc.Load();

        loaded.AsrServerPort.Should().Be(9999);
        File.Delete(path);
    }

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        var svc = new SettingsService("/nonexistent/settings.json");
        var settings = svc.Load();
        settings.Should().Be(AppSettings.Default);
    }
}
