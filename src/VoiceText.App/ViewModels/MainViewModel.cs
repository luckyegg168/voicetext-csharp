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

    private bool _isCapturing;
    private readonly List<float> _rawBuffer = new();
    private int _chunkCount;  // counts audio chunks received since last StartRecording

    [ObservableProperty] private RecordingState _state = RecordingState.Idle;
    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _polishedText = "";
    [ObservableProperty] private string _statusMessage = "準備就緒";
    [ObservableProperty] private float _audioLevel = 0f;
    [ObservableProperty] private bool _isPolishEnabled = true;
    [ObservableProperty] private bool _isTranslateEnabled = false;
    [ObservableProperty] private string _selectedLanguage = "auto";

    public event Action? OpenSettingsRequested;
    public event Action? OpenHistoryRequested;
    /// <summary>Fired with the final text after transcription (for auto-send to window).</summary>
    public event Action<string>? TranscriptionReady;

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
            _chunkCount++;
            if (_getSettings().VadEnabled)
                _vad.Feed(chunk);
            else
                lock (_rawBuffer) { _rawBuffer.AddRange(chunk.Samples); }

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
        if (_isCapturing) StopRecording();
        else StartRecording();
    }

    /// <summary>Starts recording only when idle (used by push-to-talk hotkey down).</summary>
    public void StartRecordingIfIdle()
    {
        if (!_isCapturing) StartRecording();
    }

    /// <summary>Stops recording and flushes (used by push-to-talk hotkey up).</summary>
    public void StopAndFlush() => StopRecording();

    private void StartRecording()
    {
        _isCapturing = true;
        _chunkCount = 0;
        lock (_rawBuffer) { _rawBuffer.Clear(); }
        State = RecordingState.Recording;
        StatusMessage = "錄音中...";
        RawText = "";
        PolishedText = "";
        try
        {
            var micId = _getSettings().MicrophoneDeviceId;
            _audio.StartCapture(string.IsNullOrEmpty(micId) ? null : micId);
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            State = RecordingState.Error;
            StatusMessage = $"⚠ 麥克風錯誤: {ex.Message}";
            return;
        }
        // Check after 3 s whether any audio arrived
        _ = CheckAudioInputAsync();
    }

    private async Task CheckAudioInputAsync()
    {
        await Task.Delay(3000);
        if (_isCapturing && _chunkCount == 0)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                StatusMessage = "⚠ 無音訊輸入，請檢查麥克風權限");
        }
    }

    private void StopRecording()
    {
        _isCapturing = false;
        _audio.StopCapture();

        if (!_getSettings().VadEnabled)
        {
            float[] segment;
            lock (_rawBuffer) { segment = _rawBuffer.ToArray(); _rawBuffer.Clear(); }
            if (segment.Length > 0)
                _ = ProcessSegmentAsync(segment);
            else
            {
                State = RecordingState.Idle;
                StatusMessage = "準備就緒";
            }
        }
        else
        {
            var pending = _vad.FlushIfAny();
            if (pending != null)
                _ = ProcessSegmentAsync(pending);
            else
            {
                State = RecordingState.Idle;
                StatusMessage = "準備就緒";
            }
        }
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

            var finalText = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
            if (!string.IsNullOrEmpty(finalText))
            {
                if (_getSettings().AutoCopyToClipboard)
                    System.Windows.Clipboard.SetText(finalText);
                TranscriptionReady?.Invoke(finalText);
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
