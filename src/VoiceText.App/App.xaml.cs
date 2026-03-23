using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;
using VoiceText.App.Helpers;
using VoiceText.App.ViewModels;
using VoiceText.App.Views;
using VoiceText.Asr;
using VoiceText.Audio;
using VoiceText.Config;
using VoiceText.Llm;
using VoiceText.Storage;

namespace VoiceText.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private GlobalHotkeyHelper? _hotkey;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "settings.json");
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VoiceText", "history.db");

        var settingsService = new SettingsService(settingsPath);
        var settings = settingsService.Load();

        var services = new ServiceCollection();

        // Config
        services.AddSingleton(settingsService);
        services.AddSingleton<ApiKeyStore>();
        services.AddSingleton<MicrophoneEnumerator>();
        services.AddSingleton<Func<AppSettings>>(() => settingsService.Load());

        // Audio
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        var vadModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "silero_vad.onnx");
        services.AddSingleton(_ => new VadEngine(vadModelPath));
        services.AddSingleton(sp => new VadPipeline(
            sp.GetRequiredService<VadEngine>(),
            settingsService.Load().VadSilenceTimeoutMs));

        // ASR
        services.AddHttpClient<IAsrService, QwenAsrHttpService>(c =>
            c.BaseAddress = new Uri($"http://{settings.AsrServerHost}:{settings.AsrServerPort}"));
        var asrServerDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "asr_server");
        services.AddSingleton(_ => new AsrServerManager("python", asrServerDir, settings.AsrServerPort));

        // LLM
        services.AddHttpClient<OllamaService>(c => c.BaseAddress = new Uri(settings.OllamaBaseUrl));
        services.AddHttpClient<LlamaCppService>(c => c.BaseAddress = new Uri(settings.LlamaCppBaseUrl));
        services.AddSingleton<ILlmService>(sp => new LlmRouter(
            () => settingsService.Load(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<LlamaCppService>()));
        services.AddSingleton<PolishService>();
        services.AddSingleton<TranslationService>();

        // Storage
        services.AddSingleton<IHistoryRepository>(_ => new HistoryRepository(dbPath));

        // ViewModels & Views
        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<HistoryWindow>();

        Services = services.BuildServiceProvider();

        // Start ASR server
        var serverManager = Services.GetRequiredService<AsrServerManager>();
        serverManager.Start();

        // Show main window
        var main = Services.GetRequiredService<MainWindow>();
        main.Show();

        // Register global hotkey after window is rendered
        main.Loaded += (_, _) =>
        {
            _hotkey = new GlobalHotkeyHelper();
            _hotkey.Register(main, HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x56 /* V */);
            _hotkey.HotkeyPressed += (_, _) =>
            {
                main.Show();
                main.Activate();
                var vm = (MainViewModel)main.DataContext;
                vm.ToggleRecordingCommand.Execute(null);
            };
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        Services.GetService<AsrServerManager>()?.Stop();
        base.OnExit(e);
    }
}

