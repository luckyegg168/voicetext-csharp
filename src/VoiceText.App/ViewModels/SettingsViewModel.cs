// src/VoiceText.App/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Config;
using VoiceText.Llm;

namespace VoiceText.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private const string FixedGlobalHotkey = "Ctrl+Alt+F8";
    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly MicrophoneEnumerator _micEnum;
    private readonly OllamaModelCatalog _ollamaModelCatalog;

    [ObservableProperty] private string _asrServerHost = "127.0.0.1";
    [ObservableProperty] private int _asrServerPort = 8765;
    [ObservableProperty] private string _asrModelId = "Qwen/Qwen3-ASR-0.6B";
    [ObservableProperty] private string _llmBackend = "Ollama";
    [ObservableProperty] private string _ollamaBaseUrl = "http://localhost:11434/api";
    [ObservableProperty] private string _llamaCppBaseUrl = "http://localhost:8080";
    [ObservableProperty] private string _llmModelName = "gemma:2b";
    [ObservableProperty] private string _polishPromptStyle = "natural";
    [ObservableProperty] private string _polishOutputLanguage = "none";
    [ObservableProperty] private double _vadSilenceTimeoutMs = 300;
    [ObservableProperty] private double _vadMinSpeechMs = 30;
    [ObservableProperty] private double _vadMinVolumePercent = 0.2;
    [ObservableProperty] private double _vadSpeechThreshold = 0.35;
    [ObservableProperty] private bool _autoCopyToClipboard = true;
    [ObservableProperty] private bool _autoSendToWindow = false;
    [ObservableProperty] private bool _pushToTalkMode = false;
    [ObservableProperty] private string _globalHotkey = "Ctrl+Alt+F8";
    [ObservableProperty] private bool _vadEnabled = true;
    [ObservableProperty] private IReadOnlyList<string> _availableLlmModels = [];
    [ObservableProperty] private IReadOnlyList<MicrophoneDevice> _microphones = [];
    [ObservableProperty] private string _selectedMicrophoneId = "";

    public IReadOnlyList<string> LlmBackends { get; } = ["Ollama", "LlamaCpp"];
    public IReadOnlyList<string> PolishStyles { get; } = ["natural", "formal", "technical", "meeting"];
    public IReadOnlyList<string> AsrModels { get; } = ["Qwen/Qwen3-ASR-0.6B", "Qwen/Qwen3-ASR-1.7B"];

    public SettingsViewModel(SettingsService settingsService, ApiKeyStore keyStore, MicrophoneEnumerator micEnum, OllamaModelCatalog ollamaModelCatalog)
    {
        _settingsService = settingsService;
        _keyStore = keyStore;
        _micEnum = micEnum;
        _ollamaModelCatalog = ollamaModelCatalog;
        LoadFromSettings(settingsService.Load());
        Microphones = micEnum.GetDevices();
        AvailableLlmModels = [LlmModelName];
        // Default to first device if none saved
        if (string.IsNullOrEmpty(SelectedMicrophoneId) && Microphones.Count > 0)
            SelectedMicrophoneId = Microphones[0].Id;
        _ = LoadAvailableModelsAsync();
    }

    private void LoadFromSettings(AppSettings s)
    {
        AsrServerHost = s.AsrServerHost;
        AsrServerPort = s.AsrServerPort;
        AsrModelId = s.AsrModelId;
        LlmBackend = s.LlmBackend;
        OllamaBaseUrl = NormalizeOllamaBaseUrl(s.OllamaBaseUrl);
        LlamaCppBaseUrl = s.LlamaCppBaseUrl;
        LlmModelName = s.LlmModelName;
        PolishPromptStyle = s.PolishPromptStyle;
        PolishOutputLanguage = s.PolishOutputLanguage;
        VadSilenceTimeoutMs = s.VadSilenceTimeoutMs;
        VadMinSpeechMs = s.VadMinSpeechMs;
        VadMinVolumePercent = s.VadMinVolumePercent;
        VadSpeechThreshold = s.VadSpeechThreshold;
        VadEnabled = s.VadEnabled;
        AutoCopyToClipboard = s.AutoCopyToClipboard;
        AutoSendToWindow = s.AutoSendToWindow;
        PushToTalkMode = s.PushToTalkMode;
        GlobalHotkey = FixedGlobalHotkey;
        SelectedMicrophoneId = s.MicrophoneDeviceId;
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new AppSettings
        {
            AsrServerHost = AsrServerHost,
            AsrServerPort = AsrServerPort,
            AsrModelId = AsrModelId,
            LlmBackend = LlmBackend,
            OllamaBaseUrl = NormalizeOllamaBaseUrl(OllamaBaseUrl),
            LlamaCppBaseUrl = LlamaCppBaseUrl,
            LlmModelName = LlmModelName,
            PolishPromptStyle = PolishPromptStyle,
            PolishOutputLanguage = PolishOutputLanguage,
            VadSilenceTimeoutMs = VadSilenceTimeoutMs,
            VadMinSpeechMs = VadMinSpeechMs,
            VadMinVolumePercent = VadMinVolumePercent,
            VadSpeechThreshold = VadSpeechThreshold,
            VadEnabled = VadEnabled,
            AutoCopyToClipboard = AutoCopyToClipboard,
            AutoSendToWindow = AutoSendToWindow,
            PushToTalkMode = PushToTalkMode,
            GlobalHotkey = FixedGlobalHotkey,
            MicrophoneDeviceId = SelectedMicrophoneId,
        };
        _settingsService.Save(settings);
    }

    private static string NormalizeOllamaBaseUrl(string? value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "http://localhost:11434/api/" : value.Trim();
        if (!raw.EndsWith("/", StringComparison.Ordinal))
            raw += "/";

        if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            var path = uri.AbsolutePath.TrimEnd('/');
            if (!path.EndsWith("/api", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                var builder = new UriBuilder(uri)
                {
                    Path = $"{path}/api/".Replace("//", "/")
                };
                return builder.Uri.ToString();
            }

            return uri.ToString();
        }

        return "http://localhost:11434/api/";
    }

    private async Task LoadAvailableModelsAsync()
    {
        var models = await _ollamaModelCatalog.GetModelNamesAsync();
        if (models.Count == 0)
            return;

        AvailableLlmModels = models.Contains(LlmModelName, StringComparer.OrdinalIgnoreCase)
            ? models
            : [.. models, LlmModelName];
    }
}
