using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using WinForms = System.Windows.Forms;
using VoiceText.App.Helpers;
using VoiceText.App.ViewModels;
using VoiceText.App.Views;
using VoiceText.Asr;
using VoiceText.Audio;
using VoiceText.Config;
using VoiceText.Llm;
using VoiceText.Storage;

namespace VoiceText.App;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private GlobalHotkeyHelper? _hotkey;
    private IDisposable? _trayIcon;   // runtime type: WinForms.NotifyIcon

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var culture = new CultureInfo("zh-TW");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("zh-TW")));

        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            var msg = ex.ExceptionObject?.ToString() ?? "Unknown error";
            File.WriteAllText("voicetext_crash.log", msg);
            MessageBox.Show(msg, "VoiceText Crash", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        DispatcherUnhandledException += (_, ex) =>
        {
            var msg = ex.Exception?.ToString() ?? "Unknown error";
            File.WriteAllText("voicetext_crash.log", msg);
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

        services.AddSingleton(settingsService);
        services.AddSingleton<ApiKeyStore>();
        services.AddSingleton<MicrophoneEnumerator>();
        services.AddSingleton<Func<AppSettings>>(() => settingsService.Load());

        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        var vadModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "silero_vad.onnx");
        services.AddSingleton(_ => new VadEngine(vadModelPath));
        services.AddSingleton(sp => new VadPipeline(
            sp.GetRequiredService<VadEngine>(),
            settingsService.Load().VadSilenceTimeoutMs));

        services.AddHttpClient<IAsrService, QwenAsrHttpService>(c =>
            c.BaseAddress = new Uri($"http://{settings.AsrServerHost}:{settings.AsrServerPort}"));

        // workingDir = PARENT of asr_server/ so "python -m asr_server.main" resolves
        var asrServerDir = FindAncestorDirectory("asr_server", AppDomain.CurrentDomain.BaseDirectory)
            ?? throw new DirectoryNotFoundException("Cannot locate asr_server directory.");
        var asrWorkingDir = Path.GetDirectoryName(asrServerDir)
            ?? throw new DirectoryNotFoundException("Cannot determine parent of asr_server directory.");
        var venvPython = Path.Combine(asrServerDir, ".venv", "Scripts", "python.exe");
        var pythonExe = File.Exists(venvPython) ? venvPython : "python";
        services.AddSingleton(_ => new AsrServerManager(pythonExe, asrWorkingDir, settings.AsrServerPort));

        services.AddHttpClient<OllamaService>(c => c.BaseAddress = new Uri(settings.OllamaBaseUrl));
        services.AddHttpClient<LlamaCppService>(c => c.BaseAddress = new Uri(settings.LlamaCppBaseUrl));
        services.AddSingleton<ILlmService>(sp => new LlmRouter(
            () => settingsService.Load(),
            sp.GetRequiredService<OllamaService>(),
            sp.GetRequiredService<LlamaCppService>()));
        services.AddSingleton<PolishService>();
        services.AddSingleton<TranslationService>();
        services.AddSingleton<IHistoryRepository>(_ => new HistoryRepository(dbPath));

        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
        services.AddTransient<HistoryWindow>();

        Services = services.BuildServiceProvider();

        var serverManager = Services.GetRequiredService<AsrServerManager>();
        serverManager.Start();

        var main = Services.GetRequiredService<MainWindow>();

        // Wire navigation events
        if (main.DataContext is MainViewModel vm)
        {
            vm.OpenSettingsRequested += () =>
            {
                var w = Services.GetRequiredService<SettingsWindow>();
                w.Owner = main;
                w.ShowDialog();
            };
            vm.OpenHistoryRequested += () =>
            {
                var w = Services.GetRequiredService<HistoryWindow>();
                w.Owner = main;
                w.Show();
                w.Activate();
            };
        }

        main.Show();

        main.Loaded += (_, _) => OnMainLoaded(main, serverManager);
    }

    private void OnMainLoaded(MainWindow main, AsrServerManager serverManager)
    {
        // Global hotkey
        _hotkey = new GlobalHotkeyHelper();
        _hotkey.Register(main, HotkeyModifiers.Alt | HotkeyModifiers.Shift, 0x56 /* V */);
        _hotkey.HotkeyPressed += (_, _) =>
        {
            main.Show();
            main.Activate();
            ((MainViewModel)main.DataContext).ToggleRecordingCommand.Execute(null);
        };

        // System tray
        SetupTray(main);

        // Wait for ASR server (fire-and-forget, update status when done)
        if (main.DataContext is MainViewModel vm)
        {
            vm.StatusMessage = "等待 ASR 伺服器啟動...";
            serverManager.WaitForReadyAsync(60_000).ContinueWith(t =>
                Dispatcher.InvokeAsync(() =>
                    vm.StatusMessage = t.Result ? "準備就緒" : "⚠ ASR 伺服器未回應"));
        }
    }

    private void SetupTray(MainWindow main)
    {
        var icon = new WinForms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "VoiceText",
            Visible = true,
        };
        var menu = new WinForms.ContextMenuStrip();
        var showItem = new WinForms.ToolStripMenuItem("顯示主視窗");
        showItem.Click += (_, _) => { main.Show(); main.Activate(); };
        var exitItem = new WinForms.ToolStripMenuItem("結束 VoiceText");
        exitItem.Click += (_, _) => { icon.Visible = false; Shutdown(); };
        menu.Items.Add(showItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        icon.ContextMenuStrip = menu;
        icon.DoubleClick += (_, _) => { main.Show(); main.Activate(); };
        _trayIcon = icon;
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
        _trayIcon?.Dispose();
        _hotkey?.Dispose();
        Services.GetService<AsrServerManager>()?.Stop();
        base.OnExit(e);
    }
}
