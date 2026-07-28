using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScreenTranslator.App.ViewModels;

public sealed class GeneralSettingsViewModel : ObservableObject
{
    private bool _startWithWindows;
    private bool _minimizeToTray = true;
    private string _targetLanguage = "简体中文";

    public GeneralSettingsViewModel()
    {
        StartCaptureCommand = new RelayCommand(
            () => StartCaptureRequested?.Invoke(this, EventArgs.Empty));
    }

    public ObservableCollection<string> TargetLanguages { get; } =
        ["简体中文", "繁体中文", "英语", "日语", "韩语"];

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => SetProperty(ref _startWithWindows, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => SetProperty(ref _minimizeToTray, value);
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set => SetProperty(ref _targetLanguage, value);
    }

    public IRelayCommand StartCaptureCommand { get; }

    public event EventHandler? StartCaptureRequested;
}

public sealed class AppearanceSettingsViewModel : ObservableObject
{
    private string _theme = "跟随系统";
    private string _displayMode = "原文旁边";
    private double _panelOpacity = 94;
    private bool _useAnimations = true;

    public ObservableCollection<string> Themes { get; } =
        ["跟随系统", "浅色", "深色"];

    public ObservableCollection<string> DisplayModes { get; } =
        ["原文旁边", "原位覆盖"];

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public string DisplayMode
    {
        get => _displayMode;
        set => SetProperty(ref _displayMode, value);
    }

    public double PanelOpacity
    {
        get => _panelOpacity;
        set => SetProperty(ref _panelOpacity, value);
    }

    public bool UseAnimations
    {
        get => _useAnimations;
        set => SetProperty(ref _useAnimations, value);
    }
}

public sealed class HotkeySettingsViewModel : ObservableObject
{
    private string _hotkeyText = "Alt + Shift + T";
    private bool _isEnabled = true;
    private string _statusText = "快捷键可用";

    public HotkeySettingsViewModel()
    {
        RecordCommand = new RelayCommand(
            () => RecordRequested?.Invoke(this, EventArgs.Empty));
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => SetProperty(ref _hotkeyText, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public IRelayCommand RecordCommand { get; }

    public event EventHandler? RecordRequested;
}

public sealed class PrivacySettingsViewModel : ObservableObject
{
    private bool _saveTextHistory;

    public PrivacySettingsViewModel()
    {
        ClearHistoryCommand = new RelayCommand(
            () => ClearHistoryRequested?.Invoke(this, EventArgs.Empty));
    }

    public bool SaveTextHistory
    {
        get => _saveTextHistory;
        set => SetProperty(ref _saveTextHistory, value);
    }

    public IRelayCommand ClearHistoryCommand { get; }

    public event EventHandler? ClearHistoryRequested;
}
