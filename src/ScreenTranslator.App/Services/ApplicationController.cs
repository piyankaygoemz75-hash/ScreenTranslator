using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Windows.Input;
using Microsoft.Win32;
using ScreenTranslator.App.Services.Capture;
using ScreenTranslator.App.Services.Hotkeys;
using ScreenTranslator.App.Services.Ocr;
using ScreenTranslator.App.Services.Settings;
using ScreenTranslator.App.Services.Tray;
using ScreenTranslator.App.ViewModels;
using ScreenTranslator.App.Windows;
using ScreenTranslator.Core.Abstractions;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Sessions;
using ScreenTranslator.Core.Settings;
using ScreenTranslator.Core.Translation;
using Wpf.Ui.Appearance;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using Point = System.Windows.Point;
using Rect = System.Windows.Rect;
using Window = System.Windows.Window;

namespace ScreenTranslator.App.Services;

public sealed class ApplicationController : IDisposable
{
    private readonly Application _application;
    private readonly ISettingsStore _settingsStore;
    private readonly ISecretStore _secretStore;
    private readonly IScreenCaptureService _screenCapture;
    private readonly IOcrEngine _ocrEngine;
    private readonly IGlobalHotkeyService _hotkey;
    private readonly TrayIconService _tray;
    private readonly TranslationSessionCoordinator _sessions = new();
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly List<Window> _resultWindows = [];

    private MainWindow? _mainWindow;
    private AppSettings _persistedSettings = new();
    private LastTranslationWork? _lastWork;
    private bool _overlaysVisible = true;
    private bool _applyingSettings;
    private bool _disposed;

    public ApplicationController(Application application)
    {
        _application = application;
        _settingsStore = new JsonSettingsStore();
        _secretStore = new DpapiSecretStore();
        _screenCapture = new FallbackScreenCaptureService();
        _ocrEngine = new WindowsOcrEngine();
        _hotkey = new GlobalHotkeyService();
        _tray = new TrayIconService();

        MainWindow = new MainWindowViewModel();
        GeneralSettings = new GeneralSettingsViewModel();
        TranslationSettings = new TranslationSettingsViewModel();
        AppearanceSettings = new AppearanceSettingsViewModel();
        HotkeySettings = new HotkeySettingsViewModel();
        PrivacySettings = new PrivacySettingsViewModel();
    }

    public MainWindowViewModel MainWindow { get; }

    public GeneralSettingsViewModel GeneralSettings { get; }

    public TranslationSettingsViewModel TranslationSettings { get; }

    public AppearanceSettingsViewModel AppearanceSettings { get; }

    public HotkeySettingsViewModel HotkeySettings { get; }

    public PrivacySettingsViewModel PrivacySettings { get; }

    public async Task InitializeAsync()
    {
        _persistedSettings = await _settingsStore.LoadAsync();
        ApplySettings(_persistedSettings);

        MainWindow.StartCaptureRequested += OnCaptureRequested;
        GeneralSettings.StartCaptureRequested += OnCaptureRequested;
        GeneralSettings.PropertyChanged += OnSettingsPropertyChanged;
        TranslationSettings.PropertyChanged += OnSettingsPropertyChanged;
        AppearanceSettings.PropertyChanged += OnSettingsPropertyChanged;
        HotkeySettings.PropertyChanged += OnSettingsPropertyChanged;
        PrivacySettings.PropertyChanged += OnSettingsPropertyChanged;
        TranslationSettings.SaveRequested += OnTranslationSettingsSaveRequested;
        TranslationSettings.ConnectionTester = TestConnectionAsync;
        HotkeySettings.RecordRequested += OnHotkeyRecordRequested;
        PrivacySettings.ClearHistoryRequested += OnClearHistoryRequested;

        _hotkey.CaptureRequested += OnCaptureRequested;
        _tray.CaptureRequested += OnCaptureRequested;
        _tray.ToggleOverlaysRequested += (_, _) => ToggleOverlays();
        _tray.ShowSettingsRequested += (_, _) => ShowSettings();
        _tray.HotkeyPauseChanged += OnHotkeyPauseChanged;
        _tray.ExitRequested += (_, _) => Exit();

        RegisterDefaultHotkey();
        ApplyStartupRegistration(_persistedSettings.StartWithWindows);
        MainWindow.StatusText = "准备就绪 · Alt + Shift + T";
    }

    public void ShowSettings()
    {
        if (_mainWindow is null || !_mainWindow.IsLoaded)
        {
            _mainWindow = new MainWindow(MainWindow);
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }

        if (!_mainWindow.IsVisible)
        {
            _mainWindow.Show();
        }

        if (_mainWindow.WindowState == System.Windows.WindowState.Minimized)
        {
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void OnCaptureRequested(object? sender, EventArgs e) =>
        _ = CaptureAndTranslateAsync();

    private async Task CaptureAndTranslateAsync()
    {
        if (!await _captureGate.WaitAsync(0))
        {
            MainWindow.StatusText = "正在处理上一项翻译";
            return;
        }

        TranslationSession? session = null;
        try
        {
            session = _sessions.Start();
            MainWindow.IsCaptureAvailable = false;
            MainWindow.StatusText = "正在截取屏幕…";

            CloseResultWindows();
            _mainWindow?.Hide();
            await _application.Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);

            var captures = await _screenCapture.CaptureAllAsync(session.CancellationToken);
            if (captures.Count == 0)
            {
                throw new InvalidOperationException("没有检测到可用的显示器。");
            }

            MainWindow.StatusText = "请框选要翻译的文字";
            var selection = await SelectRegionAsync(captures, session.CancellationToken);
            if (selection is null)
            {
                _sessions.Cancel(session.Id);
                MainWindow.StatusText = "已取消框选";
                return;
            }

            _sessions.TryTransition(session.Id, TranslationSessionState.Ocr);
            MainWindow.StatusText = "正在本地识别文字…";

            var relativeBounds = new PixelRect(
                selection.Bounds.X - selection.Capture.Monitor.Bounds.X,
                selection.Bounds.Y - selection.Capture.Monitor.Bounds.Y,
                selection.Bounds.Width,
                selection.Bounds.Height);
            relativeBounds = relativeBounds.Intersect(
                new PixelRect(
                    0,
                    0,
                    selection.Capture.Bitmap.Width,
                    selection.Capture.Bitmap.Height));

            if (!relativeBounds.IsUsable)
            {
                throw new InvalidOperationException("框选区域太小，请重新选择。");
            }

            var cropped = CapturedBitmapCropper.Crop(
                selection.Capture.Bitmap,
                relativeBounds);
            var settings = CollectSettings();
            var blocks = await _ocrEngine.RecognizeAsync(
                cropped,
                settings.SourceLanguage,
                session.CancellationToken);

            if (blocks.Count == 0)
            {
                throw new InvalidOperationException(
                    "框选区域内没有识别到文字。可尝试放大内容或安装对应的 Windows OCR 语言包。");
            }

            _lastWork = new LastTranslationWork(
                selection.Capture.Monitor,
                selection.Bounds,
                blocks);

            _sessions.TryTransition(session.Id, TranslationSessionState.Translating);
            await TranslateAndShowAsync(_lastWork, settings, session);
        }
        catch (OperationCanceledException)
        {
            MainWindow.StatusText = "翻译已取消";
        }
        catch (Exception exception)
        {
            if (session is not null)
            {
                _sessions.TryTransition(session.Id, TranslationSessionState.Failed);
            }

            MainWindow.StatusText = "翻译失败";
            ShowFailure(exception.Message);
        }
        finally
        {
            MainWindow.IsCaptureAvailable = true;
            _captureGate.Release();
        }
    }

    private async Task TranslateAndShowAsync(
        LastTranslationWork work,
        AppSettings settings,
        TranslationSession session)
    {
        MainWindow.StatusText = "正在调用 DeepSeek 翻译…";
        using var httpClient = new HttpClient();
        var provider = new DeepSeekTranslationProvider(
            httpClient,
            _secretStore,
            CreateDeepSeekOptions(settings));
        var orchestrator = new TranslationOrchestrator(
            provider,
            new TranslationResponseValidator());
        var request = new TranslationRequest(
            settings.SourceLanguage,
            settings.TargetLanguage,
            settings.TranslationStyle,
            settings.TranslationContext,
            work.Blocks);
        var result = await orchestrator.TranslateAsync(request, session.CancellationToken);

        if (!_sessions.TryPublish(session.Id))
        {
            return;
        }

        ShowTranslation(work, result, settings);
        _sessions.TryTransition(session.Id, TranslationSessionState.Displayed);
        MainWindow.StatusText = $"翻译完成 · {result.Blocks.Count} 个文本块";
    }

    private async Task RetryLastAsync()
    {
        if (_lastWork is null || !await _captureGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var session = _sessions.Start();
            MainWindow.IsCaptureAvailable = false;
            await TranslateAndShowAsync(_lastWork, CollectSettings(), session);
        }
        catch (Exception exception)
        {
            MainWindow.StatusText = "重译失败";
            ShowFailure(exception.Message);
        }
        finally
        {
            MainWindow.IsCaptureAvailable = true;
            _captureGate.Release();
        }
    }

    private async Task<SelectedRegion?> SelectRegionAsync(
        IReadOnlyList<MonitorCapture> captures,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<SelectedRegion?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var windows = new List<SelectionOverlayWindow>(captures.Count);

        foreach (var capture in captures)
        {
            var monitor = capture.Monitor;
            var window = new SelectionOverlayWindow();
            window.Configure(
                capture.Preview,
                new Rect(
                    monitor.Bounds.X / monitor.ScaleX,
                    monitor.Bounds.Y / monitor.ScaleY,
                    monitor.Bounds.Width / monitor.ScaleX,
                    monitor.Bounds.Height / monitor.ScaleY),
                new Point(monitor.Bounds.X, monitor.Bounds.Y),
                monitor.ScaleX,
                monitor.ScaleY);
            window.SelectionCompleted += (_, args) =>
            {
                completion.TrySetResult(
                    new SelectedRegion(
                        capture,
                        new PixelRect(
                            args.BoundsInPhysicalPixels.X,
                            args.BoundsInPhysicalPixels.Y,
                            args.BoundsInPhysicalPixels.Width,
                            args.BoundsInPhysicalPixels.Height)));
            };
            window.SelectionCancelled += (_, _) => completion.TrySetResult(null);
            window.Closed += (_, _) =>
            {
                if (windows.All(candidate => !candidate.IsVisible))
                {
                    completion.TrySetResult(null);
                }
            };
            windows.Add(window);
        }

        using var cancellationRegistration = cancellationToken.Register(
            () => completion.TrySetCanceled(cancellationToken));

        foreach (var window in windows.OrderBy(window => window.Left))
        {
            window.Show();
        }

        try
        {
            return await completion.Task;
        }
        finally
        {
            foreach (var window in windows)
            {
                if (window.IsLoaded)
                {
                    window.Close();
                }
            }
        }
    }

    private void ShowTranslation(
        LastTranslationWork work,
        TranslationResult result,
        AppSettings settings)
    {
        CloseResultWindows();
        _overlaysVisible = true;

        if (settings.DisplayMode == DisplayMode.Overlay)
        {
            ShowTextOverlays(work, result, settings);
        }
        else
        {
            ShowSidePanel(work, result, settings);
        }
    }

    private void ShowSidePanel(
        LastTranslationWork work,
        TranslationResult result,
        AppSettings settings)
    {
        var viewModel = CreateResultViewModel(result, settings);
        var panel = new SidePanelWindow(viewModel)
        {
            Opacity = Math.Clamp(settings.OverlayOpacity, 0.72, 1),
        };
        viewModel.RetryRequested += (_, _) => _ = RetryLastAsync();
        viewModel.SwitchModeRequested += (_, _) =>
        {
            CloseResultWindows();
            ShowTextOverlays(
                work,
                result,
                settings with { DisplayMode = DisplayMode.Overlay });
        };

        var monitor = work.Monitor;
        panel.PlaceBeside(
            ToDipRect(work.AbsoluteSelection, monitor),
            ToDipRect(monitor.WorkArea, monitor));
        TrackAndShow(panel);
    }

    private void ShowTextOverlays(
        LastTranslationWork work,
        TranslationResult result,
        AppSettings settings)
    {
        foreach (var block in result.Blocks)
        {
            var viewModel = new TranslationResultViewModel
            {
                SourceText = block.SourceText,
                TranslatedText = block.Translation,
                SourceLanguageLabel = LanguageLabel(settings.SourceLanguage),
                TargetLanguageLabel = LanguageLabel(settings.TargetLanguage),
            };
            var overlay = new TextOverlayWindow(viewModel)
            {
                Opacity = Math.Clamp(settings.OverlayOpacity, 0.72, 1),
            };
            var absoluteBounds = block.Bounds.Translate(
                new PixelPoint(work.AbsoluteSelection.X, work.AbsoluteSelection.Y));
            overlay.SetBounds(ToDipRect(absoluteBounds, work.Monitor));
            var estimatedFontSize = Math.Clamp(
                absoluteBounds.Height / work.Monitor.ScaleY * 0.48,
                settings.MinimumOverlayFontSize,
                settings.MaximumOverlayFontSize);
            overlay.SetTextStyle(estimatedFontSize);
            overlay.SetInteractive(true);
            viewModel.RetryRequested += (_, _) => _ = RetryLastAsync();
            viewModel.SwitchModeRequested += (_, _) =>
            {
                CloseResultWindows();
                ShowSidePanel(
                    work,
                    result,
                    settings with { DisplayMode = DisplayMode.SidePanel });
            };
            TrackAndShow(overlay);
        }
    }

    private static TranslationResultViewModel CreateResultViewModel(
        TranslationResult result,
        AppSettings settings) =>
        new()
        {
            SourceText = string.Join(Environment.NewLine, result.Blocks.Select(block => block.SourceText)),
            TranslatedText = string.Join(Environment.NewLine, result.Blocks.Select(block => block.Translation)),
            SourceLanguageLabel = LanguageLabel(settings.SourceLanguage),
            TargetLanguageLabel = LanguageLabel(settings.TargetLanguage),
        };

    private void TrackAndShow(Window window)
    {
        _resultWindows.Add(window);
        window.Closed += (_, _) => _resultWindows.Remove(window);
        window.Show();
    }

    private void CloseResultWindows()
    {
        foreach (var window in _resultWindows.ToArray())
        {
            window.Close();
        }

        _resultWindows.Clear();
    }

    private void ToggleOverlays()
    {
        _overlaysVisible = !_overlaysVisible;
        foreach (var window in _resultWindows)
        {
            if (_overlaysVisible)
            {
                window.Show();
            }
            else
            {
                window.Hide();
            }
        }
    }

    private async Task<ConnectionTestResult> TestConnectionAsync(
        DeepSeekConnectionTestRequest request,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            return new ConnectionTestResult(false, "Base URL 格式无效");
        }

        var stopwatch = Stopwatch.StartNew();
        using var httpClient = new HttpClient();
        var provider = new DeepSeekTranslationProvider(
            httpClient,
            new InMemorySecretStore(request.ApiKey),
            new DeepSeekOptions
            {
                BaseUri = baseUri,
                Model = request.Model,
                Timeout = TimeSpan.FromSeconds(15),
            });
        var orchestrator = new TranslationOrchestrator(
            provider,
            new TranslationResponseValidator());
        var testRequest = new TranslationRequest(
            "zh-CN",
            "en",
            TranslationStyle.Natural,
            "Connection test. Translate concisely.",
            [new OcrBlock("block-1", "你好", 1, new PixelRect(0, 0, 32, 16), 0)]);

        try
        {
            await orchestrator.TranslateAsync(testRequest, cancellationToken);
            stopwatch.Stop();
            return new ConnectionTestResult(true, "连接成功", stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            return new ConnectionTestResult(false, exception.Message);
        }
    }

    private async void OnTranslationSettingsSaveRequested(object? sender, EventArgs e)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(TranslationSettings.ApiKey))
            {
                await _secretStore.SetAsync(
                    DeepSeekTranslationProvider.ApiKeyName,
                    TranslationSettings.ApiKey);
                TranslationSettings.ApiKey = string.Empty;
            }

            await SaveSettingsAsync();
            TranslationSettings.SetConnectionStatus("设置已安全保存");
            MainWindow.StatusText = "翻译设置已保存";
        }
        catch (Exception exception)
        {
            ShowFailure($"保存设置失败：{exception.Message}");
        }
    }

    private async void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_applyingSettings)
        {
            return;
        }

        if (sender == GeneralSettings && e.PropertyName == nameof(GeneralSettings.TargetLanguage))
        {
            _applyingSettings = true;
            TranslationSettings.TargetLanguage = GeneralSettings.TargetLanguage;
            _applyingSettings = false;
        }
        else if (sender == TranslationSettings && e.PropertyName == nameof(TranslationSettings.TargetLanguage))
        {
            _applyingSettings = true;
            GeneralSettings.TargetLanguage = TranslationSettings.TargetLanguage;
            _applyingSettings = false;
        }

        if (sender == AppearanceSettings && e.PropertyName == nameof(AppearanceSettings.Theme))
        {
            ApplyTheme(ThemeValue(AppearanceSettings.Theme));
        }
        else if (sender == GeneralSettings && e.PropertyName == nameof(GeneralSettings.StartWithWindows))
        {
            ApplyStartupRegistration(GeneralSettings.StartWithWindows);
        }

        try
        {
            await SaveSettingsAsync();
        }
        catch
        {
            MainWindow.StatusText = "设置将在退出时重试保存";
        }
    }

    private async Task SaveSettingsAsync()
    {
        var snapshot = CollectSettings();
        await _settingsGate.WaitAsync();
        try
        {
            await _settingsStore.SaveAsync(snapshot);
            _persistedSettings = snapshot;
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private void OnHotkeyPauseChanged(object? sender, bool paused)
    {
        if (paused)
        {
            _hotkey.Unregister();
            HotkeySettings.IsEnabled = false;
            HotkeySettings.StatusText = "快捷键已暂停";
        }
        else
        {
            RegisterDefaultHotkey();
        }
    }

    private void OnHotkeyRecordRequested(object? sender, EventArgs e)
    {
        HotkeySettings.HotkeyText = "Alt + Shift + T";
        HotkeySettings.StatusText = "当前版本使用默认快捷键";
    }

    private void OnClearHistoryRequested(object? sender, EventArgs e)
    {
        PrivacySettings.SaveTextHistory = false;
        MainWindow.StatusText = "本地翻译历史已清除";
    }

    private void RegisterDefaultHotkey()
    {
        try
        {
            _hotkey.Register(ModifierKeys.Alt | ModifierKeys.Shift, Key.T);
            HotkeySettings.IsEnabled = true;
            HotkeySettings.StatusText = "快捷键可用";
        }
        catch (HotkeyConflictException exception)
        {
            HotkeySettings.IsEnabled = false;
            HotkeySettings.StatusText = exception.Message;
            MainWindow.StatusText = "快捷键冲突，可从托盘开始框选";
        }
    }

    private void ApplySettings(AppSettings settings)
    {
        _applyingSettings = true;
        try
        {
            var targetLabel = LanguageLabel(settings.TargetLanguage);
            GeneralSettings.TargetLanguage = targetLabel;
            GeneralSettings.StartWithWindows = settings.StartWithWindows;
            TranslationSettings.SelectedModel = settings.DeepSeekModel;
            TranslationSettings.BaseUrl = settings.DeepSeekBaseUrl;
            TranslationSettings.SourceLanguage = LanguageLabel(settings.SourceLanguage);
            TranslationSettings.TargetLanguage = targetLabel;
            TranslationSettings.TranslationStyle = StyleLabel(settings.TranslationStyle);
            TranslationSettings.CustomContext = settings.TranslationContext;
            AppearanceSettings.Theme = ThemeLabel(settings.Theme);
            ApplyTheme(settings.Theme);
            AppearanceSettings.DisplayMode =
                settings.DisplayMode == DisplayMode.Overlay ? "原位覆盖" : "原文旁边";
            AppearanceSettings.PanelOpacity = settings.OverlayOpacity * 100;
            PrivacySettings.SaveTextHistory = settings.SaveHistory;
        }
        finally
        {
            _applyingSettings = false;
        }
    }

    private AppSettings CollectSettings() =>
        _persistedSettings with
        {
            SourceLanguage = LanguageCode(TranslationSettings.SourceLanguage),
            TargetLanguage = LanguageCode(TranslationSettings.TargetLanguage),
            TranslationStyle = StyleValue(TranslationSettings.TranslationStyle),
            TranslationContext = TranslationSettings.CustomContext,
            DeepSeekModel = TranslationSettings.SelectedModel,
            DeepSeekBaseUrl = TranslationSettings.BaseUrl.Trim(),
            DisplayMode = AppearanceSettings.DisplayMode == "原位覆盖"
                ? DisplayMode.Overlay
                : DisplayMode.SidePanel,
            Theme = ThemeValue(AppearanceSettings.Theme),
            OverlayOpacity = Math.Clamp(AppearanceSettings.PanelOpacity / 100, 0.72, 1),
            SaveHistory = false,
            StartWithWindows = GeneralSettings.StartWithWindows,
        };

    private static DeepSeekOptions CreateDeepSeekOptions(AppSettings settings)
    {
        if (!Uri.TryCreate(settings.DeepSeekBaseUrl, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            throw new TranslationConfigurationException("DeepSeek Base URL 格式无效。");
        }

        return new DeepSeekOptions
        {
            BaseUri = baseUri,
            Model = settings.DeepSeekModel,
            Timeout = TimeSpan.FromSeconds(20),
        };
    }

    private static Rect ToDipRect(PixelRect rect, ScreenMonitor monitor) =>
        new(
            rect.X / monitor.ScaleX,
            rect.Y / monitor.ScaleY,
            rect.Width / monitor.ScaleX,
            rect.Height / monitor.ScaleY);

    private static string LanguageCode(string label) =>
        label switch
        {
            "自动检测" => "auto",
            "简体中文" => "zh-CN",
            "繁体中文" => "zh-TW",
            "英语" => "en",
            "日语" => "ja",
            "韩语" => "ko",
            _ => label,
        };

    private static string LanguageLabel(string code) =>
        code switch
        {
            "auto" => "自动检测",
            "zh-CN" => "简体中文",
            "zh-TW" => "繁体中文",
            "en" => "英语",
            "ja" => "日语",
            "ko" => "韩语",
            _ => code,
        };

    private static TranslationStyle StyleValue(string label) =>
        label switch
        {
            "直译" => TranslationStyle.Literal,
            "学习模式" => TranslationStyle.Learning,
            _ => TranslationStyle.Natural,
        };

    private static string StyleLabel(TranslationStyle style) =>
        style switch
        {
            TranslationStyle.Literal => "直译",
            TranslationStyle.Learning => "学习模式",
            _ => "自然",
        };

    private static ThemePreference ThemeValue(string label) =>
        label switch
        {
            "浅色" => ThemePreference.Light,
            "深色" => ThemePreference.Dark,
            _ => ThemePreference.System,
        };

    private static string ThemeLabel(ThemePreference theme) =>
        theme switch
        {
            ThemePreference.Light => "浅色",
            ThemePreference.Dark => "深色",
            _ => "跟随系统",
        };

    private static void ApplyTheme(ThemePreference preference)
    {
        var theme = preference switch
        {
            ThemePreference.Light => ApplicationTheme.Light,
            ThemePreference.Dark => ApplicationTheme.Dark,
            _ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light,
        };
        ApplicationThemeManager.Apply(theme);
    }

    private static void ApplyStartupRegistration(bool enabled)
    {
        const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string valueName = "ScreenTranslator";
        using var runKey = Registry.CurrentUser.OpenSubKey(runKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(runKeyPath);

        if (enabled)
        {
            var executablePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法确定屏译程序路径。");
            runKey.SetValue(valueName, $"\"{executablePath}\"");
        }
        else
        {
            runKey.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }

    private static void ShowFailure(string message) =>
        MessageBox.Show(
            message,
            "屏译",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning);

    public void Exit()
    {
        if (!_disposed)
        {
            _application.Shutdown();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            _settingsStore.SaveAsync(CollectSettings()).GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown must continue even when settings are unavailable.
        }

        CloseResultWindows();
        _sessions.Dispose();
        _hotkey.Dispose();
        _tray.Dispose();
        _captureGate.Dispose();
        _settingsGate.Dispose();
    }

    private sealed record SelectedRegion(MonitorCapture Capture, PixelRect Bounds);

    private sealed record LastTranslationWork(
        ScreenMonitor Monitor,
        PixelRect AbsoluteSelection,
        IReadOnlyList<OcrBlock> Blocks);

    private sealed class InMemorySecretStore(string apiKey) : ISecretStore
    {
        public Task<string?> GetAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(apiKey);

        public Task SetAsync(
            string key,
            string value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(
            string key,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
