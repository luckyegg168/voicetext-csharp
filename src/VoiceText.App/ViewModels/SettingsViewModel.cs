// src/VoiceText.App/ViewModels/SettingsViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Config;

namespace VoiceText.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly ApiKeyStore _keyStore;
    private readonly MicrophoneEnumerator _micEnum;

    [ObservableProperty] private string _asrServerHost = "127.0.0.1";
    [ObservableProperty] private int _asrServerPort = 8765;
    [ObservableProperty] private string _asrModelId = "Qwen/Qwen3-ASR-0.6B";
    [ObservableProperty] private string _llmBackend = "Ollama";
    [ObservableProperty] private string _ollamaBaseUrl = "http://localhost:11434";
    [ObservableProperty] private string _llamaCppBaseUrl = "http://localhost:8080";
    [ObservableProperty] private string _llmModelName = "llama3.2";
    [ObservableProperty] private string _polishPromptStyle = "natural";
    [ObservableProperty] private double _vadSilenceTimeoutMs = 1500;
    [ObservableProperty] private bool _autoCopyToClipboard = true;
    [ObservableProperty] private string _globalHotkey = "Alt+Shift+V";
    [ObservableProperty] private IReadOnlyList<(string Id, string Name)> _microphones = [];
    [ObservableProperty] private string _selectedMicrophoneId = "";

    public IReadOnlyList<string> LlmBackends { get; } = ["Ollama", "LlamaCpp"];
    public IReadOnlyList<string> PolishStyles { get; } = ["natural", "formal", "technical", "meeting"];
    public IReadOnlyList<string> AsrModels { get; } = ["Qwen/Qwen3-ASR-0.6B", "Qwen/Qwen3-ASR-1.7B"];

    public SettingsViewModel(SettingsService settingsService, ApiKeyStore keyStore, MicrophoneEnumerator micEnum)
    {
        _settingsService = settingsService;
        _keyStore = keyStore;
        _micEnum = micEnum;
        LoadFromSettings(settingsService.Load());
        Microphones = micEnum.GetDevices();
    }

    private void LoadFromSettings(AppSettings s)
    {
        AsrServerHost = s.AsrServerHost;
        AsrServerPort = s.AsrServerPort;
        AsrModelId = s.AsrModelId;
        LlmBackend = s.LlmBackend;
        OllamaBaseUrl = s.OllamaBaseUrl;
        LlamaCppBaseUrl = s.LlamaCppBaseUrl;
        LlmModelName = s.LlmModelName;
        PolishPromptStyle = s.PolishPromptStyle;
        VadSilenceTimeoutMs = s.VadSilenceTimeoutMs;
        AutoCopyToClipboard = s.AutoCopyToClipboard;
        GlobalHotkey = s.GlobalHotkey;
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
            OllamaBaseUrl = OllamaBaseUrl,
            LlamaCppBaseUrl = LlamaCppBaseUrl,
            LlmModelName = LlmModelName,
            PolishPromptStyle = PolishPromptStyle,
            VadSilenceTimeoutMs = VadSilenceTimeoutMs,
            AutoCopyToClipboard = AutoCopyToClipboard,
            GlobalHotkey = GlobalHotkey,
            MicrophoneDeviceId = SelectedMicrophoneId,
        };
        _settingsService.Save(settings);
    }
}
