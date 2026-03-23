// src/VoiceText.Config/SettingsService.cs
using System.Text.Json;

namespace VoiceText.Config;

public class SettingsService
{
    private readonly string _path;
    private static readonly JsonSerializerOptions _opts = new() { WriteIndented = true };

    public SettingsService(string path)
    {
        _path = path;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
            return AppSettings.Default;
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? AppSettings.Default;
        }
        catch
        {
            return AppSettings.Default;
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, _opts));
    }
}
