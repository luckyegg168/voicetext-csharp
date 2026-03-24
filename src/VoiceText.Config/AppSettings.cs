// src/VoiceText.Config/AppSettings.cs
namespace VoiceText.Config;

public record AppSettings
{
    public string AsrServerHost { get; init; } = "127.0.0.1";
    public int AsrServerPort { get; init; } = 8765;
    public string AsrModelId { get; init; } = "Qwen/Qwen3-ASR-0.6B";
    public string AsrLanguage { get; init; } = "auto";

    public string LlmBackend { get; init; } = "Ollama";     // "Ollama" | "LlamaCpp"
    public string OllamaBaseUrl { get; init; } = "http://localhost:11434/api";
    public string LlamaCppBaseUrl { get; init; } = "http://localhost:8080";
    public string LlmModelName { get; init; } = "gemma:2b";
    public string PolishPromptStyle { get; init; } = "natural";  // natural|formal|technical|meeting
    public string PolishOutputLanguage { get; init; } = "none";  // none|zh-TW|zh-CN|en|ja|ko|fr|es|de|pt|ru|vi|th|ar|id

    public string MicrophoneDeviceId { get; init; } = "";
    public double VadSilenceTimeoutMs { get; init; } = 300;
    public double VadMinSpeechMs { get; init; } = 30;
    public double VadMinVolumePercent { get; init; } = 0.2;
    public double VadSpeechThreshold { get; init; } = 0.35;
    public bool VadEnabled { get; init; } = false;           // neural VAD (experimental)
    public bool AutoCopyToClipboard { get; init; } = true;
    public bool AutoSendToWindow { get; init; } = false;     // paste result to focused window
    public bool PushToTalkMode { get; init; } = false;       // hold hotkey = record, release = send
    public bool StartMinimized { get; init; } = false;
    public string Theme { get; init; } = "System";           // System|Light|Dark
    public string GlobalHotkey { get; init; } = "Ctrl+Alt+F8";

    public static AppSettings Default => new();
}
