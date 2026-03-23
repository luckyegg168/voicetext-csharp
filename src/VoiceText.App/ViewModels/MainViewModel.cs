// src/VoiceText.App/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Asr;
using VoiceText.Audio;
using VoiceText.Config;
using VoiceText.Llm;
using VoiceText.Storage;

namespace VoiceText.App.ViewModels;

public enum RecordingState { Idle, Recording, Transcribing, Polishing, Done, Error }

public partial class MainViewModel : ObservableObject
{
    private readonly IAudioCaptureService _audio;
    private readonly VadPipeline _vad;
    private readonly IAsrService _asr;
    private readonly PolishService _polish;
    private readonly TranslationService _translation;
    private readonly IHistoryRepository _history;
    private readonly Func<AppSettings> _getSettings;

    // Fix 5: explicit capturing flag so ToggleRecording is correct after auto-VAD fires
    private bool _isCapturing;

    [ObservableProperty] private RecordingState _state = RecordingState.Idle;
    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _polishedText = "";
    [ObservableProperty] private string _statusMessage = "準備就緒";
    [ObservableProperty] private float _audioLevel = 0f;
    [ObservableProperty] private bool _isPolishEnabled = true;
    [ObservableProperty] private bool _isTranslateEnabled = false;
    [ObservableProperty] private string _selectedLanguage = "auto";

    // Fix 3: events wired up in App.xaml.cs to open the real windows
    public event Action? OpenSettingsRequested;
    public event Action? OpenHistoryRequested;

    public MainViewModel(IAudioCaptureService audio, VadPipeline vad,
                         IAsrService asr, PolishService polish,
                         TranslationService translation, IHistoryRepository history,
                         Func<AppSettings> getSettings)
    {
        _audio = audio;
        _vad = vad;
        _asr = asr;
        _polish = polish;
        _translation = translation;
        _history = history;
        _getSettings = getSettings;
        _vad.SpeechSegmentReady += OnSpeechSegmentReady;
        _audio.ChunkAvailable += (_, chunk) =>
        {
            _vad.Feed(chunk);
            // Update audio level (RMS) on UI thread
            float sum = 0;
            foreach (var s in chunk.Samples) sum += s * s;
            var level = (float)Math.Sqrt(sum / chunk.Samples.Length);
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AudioLevel = level;
                if (State == RecordingState.Recording)
                    StatusMessage = $"錄音中... 音量:{level:P0}";
            });
        };
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        // Fix 5: use _isCapturing, not State, so auto-VAD finishing a segment
        // doesn't confuse State into thinking we're still recording.
        if (_isCapturing)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        _isCapturing = true;
        State = RecordingState.Recording;
        StatusMessage = "錄音中...";
        RawText = "";
        PolishedText = "";
        // Fix 7: pass the saved microphone device id
        var micId = _getSettings().MicrophoneDeviceId;
        _audio.StartCapture(string.IsNullOrEmpty(micId) ? null : micId);
    }

    private void StopRecording()
    {
        _isCapturing = false;
        _audio.StopCapture();
        var pending = _vad.FlushIfAny();
        if (pending != null)
            _ = ProcessSegmentAsync(pending);
        else
            State = RecordingState.Idle;
    }

    private async void OnSpeechSegmentReady(object? sender, float[] segment)
    {
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            _ = ProcessSegmentAsync(segment));
    }

    private async Task ProcessSegmentAsync(float[] segment)
    {
        try
        {
            State = RecordingState.Transcribing;
            StatusMessage = "轉錄中...";
            var result = await _asr.TranscribeAsync(segment, SelectedLanguage == "auto" ? null : SelectedLanguage);
            RawText = result.Text;

            if (IsPolishEnabled)
            {
                State = RecordingState.Polishing;
                StatusMessage = "潤稿中...";
                PolishedText = await _polish.PolishAsync(result.Text);
            }

            await _history.AddAsync(new(0, DateTime.Now, result.Text,
                IsPolishEnabled ? PolishedText : null, null, result.Language, result.DurationMs));

            State = RecordingState.Done;
            StatusMessage = "完成";

            // Fix 6: auto-copy to clipboard when setting is on
            if (_getSettings().AutoCopyToClipboard)
            {
                var text = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
                if (!string.IsNullOrEmpty(text))
                    System.Windows.Clipboard.SetText(text);
            }
        }
        catch (Exception ex)
        {
            State = RecordingState.Error;
            StatusMessage = $"錯誤: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task TranslateAsync()
    {
        var source = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
        if (string.IsNullOrEmpty(source)) return;
        StatusMessage = "翻譯中...";
        PolishedText = await _translation.TranslateToEnglishAsync(source);
        StatusMessage = "翻譯完成";
    }

    [RelayCommand]
    private void CopyToClipboard()
    {
        var text = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
        if (!string.IsNullOrEmpty(text))
            System.Windows.Clipboard.SetText(text);
    }

    [RelayCommand]
    private void OpenSettings() => OpenSettingsRequested?.Invoke();

    [RelayCommand]
    private void OpenHistory() => OpenHistoryRequested?.Invoke();
}
