// src/VoiceText.App/ViewModels/MainViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoiceText.Asr;
using VoiceText.Audio;
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

    [ObservableProperty] private RecordingState _state = RecordingState.Idle;
    [ObservableProperty] private string _rawText = "";
    [ObservableProperty] private string _polishedText = "";
    [ObservableProperty] private string _statusMessage = "準備就緒";
    [ObservableProperty] private float _audioLevel = 0f;
    [ObservableProperty] private bool _isPolishEnabled = true;
    [ObservableProperty] private bool _isTranslateEnabled = false;
    [ObservableProperty] private string _selectedLanguage = "auto";

    public MainViewModel(IAudioCaptureService audio, VadPipeline vad,
                         IAsrService asr, PolishService polish,
                         TranslationService translation, IHistoryRepository history)
    {
        _audio = audio;
        _vad = vad;
        _asr = asr;
        _polish = polish;
        _translation = translation;
        _history = history;
        _vad.SpeechSegmentReady += OnSpeechSegmentReady;
        _audio.ChunkAvailable += (_, chunk) => _vad.Feed(chunk);
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        if (State == RecordingState.Recording)
            StopRecording();
        else
            StartRecording();
    }

    private void StartRecording()
    {
        State = RecordingState.Recording;
        StatusMessage = "錄音中...";
        RawText = "";
        PolishedText = "";
        _audio.StartCapture();
    }

    private void StopRecording()
    {
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
    private void OpenSettings() { }   // Wired in App.xaml.cs

    [RelayCommand]
    private void OpenHistory() { }    // Wired in App.xaml.cs
}
