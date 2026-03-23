using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
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

        // Set zh-TW culture globally
        var culture = new CultureInfo("zh-TW");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("zh-TW")));

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "Unknown error";
            System.IO.File.WriteAllText("voicetext_crash.log", msg);
            MessageBox.Show(msg, "VoiceText Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, ex) =>
        {
            var msg = ex.Exception?.ToString() ?? "Unknown error";
            System.IO.File.WriteAllText("voicetext_crash.log", msg);
            MessageBox.Show(msg, "VoiceText Crash", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

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
        var asrServerDir = FindAncestorDirectory("asr_server", AppDomain.CurrentDomain.BaseDirectory)
            ?? throw new DirectoryNotFoundException("Cannot locate asr_server directory.");
        var venvPython = Path.Combine(asrServerDir, ".venv", "Scripts", "python.exe");
        var pythonExe = File.Exists(venvPython) ? venvPython : "python";
        services.AddSingleton(_ => new AsrServerManager(pythonExe, asrServerDir, settings.AsrServerPort));

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

    private static string? FindAncestorDirectory(string targetName, string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, targetName);
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        Services.GetService<AsrServerManager>()?.Stop();
        base.OnExit(e);
    }
}

