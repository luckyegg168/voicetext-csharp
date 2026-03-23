// src/VoiceText.Config/ApiKeyStore.cs
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VoiceText.Config;

/// <summary>
/// Stores API keys encrypted with Windows DPAPI (per-user scope).
/// Keys are stored in %APPDATA%\VoiceText\keys\{profile}.json
/// </summary>
public class ApiKeyStore
{
    private readonly string _filePath;

    public ApiKeyStore(string profile = "default")
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "keys");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, $"{profile}.json");
    }

    public void Set(string name, string value)
    {
        var store = LoadRaw();
        var encrypted = Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
        store[name] = encrypted;
        File.WriteAllText(_filePath, JsonSerializer.Serialize(store));
    }

    public string? Get(string name)
    {
        var store = LoadRaw();
        if (!store.TryGetValue(name, out var encrypted)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }

    public void Delete(string name)
    {
        var store = LoadRaw();
        store.Remove(name);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(store));
    }

    private Dictionary<string, string> LoadRaw()
    {
        if (!File.Exists(_filePath)) return new();
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_filePath)) ?? new(); }
        catch { return new(); }
    }
}
