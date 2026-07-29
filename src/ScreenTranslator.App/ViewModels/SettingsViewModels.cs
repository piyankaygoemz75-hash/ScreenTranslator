using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTranslator.Core.Hotkeys;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;

namespace ScreenTranslator.App.ViewModels;

public sealed class GeneralSettingsViewModel : ObservableObject
{
    private bool _startWithWindows;
    private bool _minimizeToTray = true;
    private string _targetLanguage = "简体中文";
    private string _captureHotkeyText = HotkeyGesture.Default.ToDisplayString();

    public GeneralSettingsViewModel(
        BrowserIntegrationViewModel? browserIntegration = null)
    {
        BrowserIntegration = browserIntegration ?? new BrowserIntegrationViewModel();
        StartCaptureCommand = new RelayCommand(
            () => StartCaptureRequested?.Invoke(this, EventArgs.Empty));
    }

    public BrowserIntegrationViewModel BrowserIntegration { get; }

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

    public string CaptureHotkeyText
    {
        get => _captureHotkeyText;
        set => SetProperty(ref _captureHotkeyText, value);
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
    private HotkeyGesture _gesture = HotkeyGesture.Default;
    private string _hotkeyText = HotkeyGesture.Default.ToDisplayString();
    private bool _isEnabled = true;
    private string _statusText = "快捷键可用";
    private bool _isRecording;

    public HotkeySettingsViewModel()
    {
        BeginRecordingCommand = new RelayCommand(BeginRecording, () => IsEnabled);
        CancelRecordingCommand = new RelayCommand(CancelRecording, () => IsRecording);
        UseDefaultCommand = new RelayCommand(
            () => Submit(HotkeyGesture.Default),
            () => IsRecording);
    }

    public string HotkeyText
    {
        get => _hotkeyText;
        set => SetProperty(ref _hotkeyText, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                BeginRecordingCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                OnPropertyChanged(nameof(RecordButtonText));
                BeginRecordingCommand.NotifyCanExecuteChanged();
                CancelRecordingCommand.NotifyCanExecuteChanged();
                UseDefaultCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string RecordButtonText => IsRecording ? "正在录制…" : "修改快捷键";

    public HotkeyGesture Gesture => _gesture;

    public RelayCommand BeginRecordingCommand { get; }

    public RelayCommand CancelRecordingCommand { get; }

    public RelayCommand UseDefaultCommand { get; }

    public event EventHandler? RecordingStarted;

    public event EventHandler? RecordingCancelled;

    public event EventHandler<HotkeyGesture>? GestureSubmitted;

    public void AcceptKeyboardInput(ModifierKeys modifiers, Key key)
    {
        if (!IsRecording)
        {
            return;
        }

        if (IsModifierKey(key))
        {
            StatusText = "请同时按下一个字母、数字或功能键";
            return;
        }

        try
        {
            var gesture = HotkeyGesture.Create(
                ToCoreModifiers(modifiers),
                ToCoreKeyName(key));
            Submit(gesture);
        }
        catch (FormatException exception)
        {
            StatusText = exception.Message;
        }
    }

    public void ApplyGesture(
        HotkeyGesture gesture,
        bool isEnabled,
        string statusText)
    {
        _gesture = gesture;
        HotkeyText = gesture.ToDisplayString();
        IsRecording = false;
        IsEnabled = isEnabled;
        StatusText = statusText;
    }

    private void BeginRecording()
    {
        if (IsRecording)
        {
            return;
        }

        IsRecording = true;
        HotkeyText = "请按新的组合键";
        StatusText = "Esc 取消，Backspace 恢复默认";
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    private void CancelRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        IsRecording = false;
        HotkeyText = _gesture.ToDisplayString();
        RecordingCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void Submit(HotkeyGesture gesture)
    {
        IsRecording = false;
        GestureSubmitted?.Invoke(this, gesture);
    }

    private static HotkeyModifiers ToCoreModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Windows;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        return result;
    }

    private static string ToCoreKeyName(Key key)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            return key.ToString();
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)key - (int)Key.D0).ToString();
        }

        if (key is >= Key.NumPad0 and <= Key.NumPad9)
        {
            return ((int)key - (int)Key.NumPad0).ToString();
        }

        return key switch
        {
            >= Key.F1 and <= Key.F12 => key.ToString(),
            Key.Left => "Left",
            Key.Right => "Right",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Space => "Space",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            _ => key.ToString(),
        };
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;
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
