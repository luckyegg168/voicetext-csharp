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
    private const int SampleRate16k = 16_000;
    private const float DisplayLevelGain = 6f;
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
    private bool _useVadForCurrentSession;

    [ObservableProperty] private RecordingState _state = RecordingState.Idle;
    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _polishedText = "";
    [ObservableProperty] private string _lastVadFilteredLength = "";
    [ObservableProperty] private string _lastAsrRawText = "";
    [ObservableProperty] private string _lastPolishResult = "";
    [ObservableProperty] private string _lastSentText = "";
    [ObservableProperty] private string _statusMessage = "準備就緒";
    [ObservableProperty] private string _vadDiagnostics = "";
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
        _audio.ChunkAvailable += (_, chunk) =>
        {
            _chunkCount++;
            lock (_rawBuffer) { _rawBuffer.AddRange(chunk.Samples); }

            // Update audio level (RMS) on UI thread
            float sum = 0;
            foreach (var s in chunk.Samples) sum += s * s;
            var rawLevel = (float)Math.Sqrt(sum / chunk.Samples.Length);
            var displayLevel = Math.Clamp(rawLevel * DisplayLevelGain, 0f, 1f);
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                AudioLevel = displayLevel;
                if (State == RecordingState.Recording)
                    StatusMessage = $"錄音中... 音量:{displayLevel:P0}";
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
        var settings = _getSettings();
        _isCapturing = true;
        _chunkCount = 0;
        _useVadForCurrentSession = settings.VadEnabled && _vad.IsAvailable;
        _vad.SilenceTimeoutMs = Math.Max(100, settings.VadSilenceTimeoutMs);
        _vad.MinVolumePercent = Math.Clamp(settings.VadMinVolumePercent, 0, 100);
        _vad.EngineThreshold = (float)Math.Clamp(settings.VadSpeechThreshold, 0.01, 0.99);
        _vad.MinSpeechDurationMs = Math.Max(20, settings.VadMinSpeechMs);
        lock (_rawBuffer) { _rawBuffer.Clear(); }
        _vad.ResetState();   // clear leftover speech buffer / pending from last session
        State = RecordingState.Recording;
        StatusMessage = _useVadForCurrentSession ? "錄音中... (VAD)" : "錄音中...";
        RawText = "";
        PolishedText = "";
        VadDiagnostics = "";
        LastVadFilteredLength = "";
        LastAsrRawText = "";
        LastPolishResult = "";
        LastSentText = "";
        try
        {
            var micId = settings.MicrophoneDeviceId;
            _audio.StartCapture(string.IsNullOrEmpty(micId) ? null : micId);
        }
        catch (Exception ex)
        {
            _isCapturing = false;
            _useVadForCurrentSession = false;
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

        if (!_useVadForCurrentSession)
        {
            float[] segment;
            lock (_rawBuffer) { segment = _rawBuffer.ToArray(); _rawBuffer.Clear(); }
            if (segment.Length > 0)
            {
                LastVadFilteredLength = $"VAD 後長度: {segment.Length} samples (VAD 關閉)";
                _ = ProcessSegmentAsync(segment);
            }
            else
            {
                State = RecordingState.Idle;
                StatusMessage = "準備就緒";
            }
        }
        else
        {
            float[] rawSegment;
            lock (_rawBuffer) { rawSegment = _rawBuffer.ToArray(); _rawBuffer.Clear(); }
            var vadResult = _vad.ExtractSpeech(rawSegment);
            var filtered = vadResult.FilteredSamples;
            VadDiagnostics = $"VAD 音量峰值:{vadResult.MaxVolumePercent:F1}%  機率峰值:{vadResult.MaxSpeechProbability:P0}  命中窗格:{vadResult.SpeechWindowCount}/{vadResult.WindowCount}";

            var speechWindowRatio = vadResult.WindowCount > 0
                ? (double)vadResult.SpeechWindowCount / vadResult.WindowCount
                : 0;
            var shouldFallbackToRaw = rawSegment.Length > 0 &&
                                      (filtered == null ||
                                       vadResult.SpeechWindowCount <= 1 ||
                                       speechWindowRatio < 0.08);

            if (filtered is { Length: > 0 } && !shouldFallbackToRaw)
            {
                LastVadFilteredLength = $"VAD 後長度: {filtered.Length} samples";
                _ = ProcessSegmentAsync(filtered);
            }
            else if (rawSegment.Length > 0)
            {
                StatusMessage = "VAD 命中不足，改送整段錄音...";
                LastVadFilteredLength = $"VAD 後長度: {rawSegment.Length} samples (fallback raw)";
                _ = ProcessSegmentAsync(rawSegment);
            }
            else
            {
                State = RecordingState.Idle;
                StatusMessage = "準備就緒";
            }
        }

        _useVadForCurrentSession = false;
    }

    private async Task ProcessSegmentAsync(float[] segment)
    {
        try
        {
            State = RecordingState.Transcribing;
            StatusMessage = "轉錄中...";
            var result = await _asr.TranscribeAsync(segment, SelectedLanguage == "auto" ? null : SelectedLanguage);
            RawText = result.Text;
            LastAsrRawText = $"ASR 原文: {result.Text}";
            var polishFailed = false;

            if (IsPolishEnabled)
            {
                try
                {
                    State = RecordingState.Polishing;
                    StatusMessage = "潤稿中...";
                    PolishedText = await _polish.PolishAsync(result.Text);
                    LastPolishResult = $"潤稿結果: {PolishedText}";
                }
                catch
                {
                    polishFailed = true;
                    PolishedText = "";
                    LastPolishResult = "潤稿結果: <failed, fallback raw>";
                }
            }
            else
            {
                LastPolishResult = "潤稿結果: <disabled>";
            }

            await _history.AddAsync(new(0, DateTime.Now, result.Text,
                IsPolishEnabled ? PolishedText : null, null, result.Language, result.DurationMs));

            State = RecordingState.Done;
            StatusMessage = polishFailed ? "完成（潤稿失敗，已保留原文）" : "完成";

            var finalText = string.IsNullOrEmpty(PolishedText) ? RawText : PolishedText;
            if (!string.IsNullOrEmpty(finalText))
            {
                if (_getSettings().AutoCopyToClipboard)
                    System.Windows.Clipboard.SetText(finalText);
                LastSentText = $"實際送出文字: {finalText}";
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
