using CommunityToolkit.Mvvm.ComponentModel;

namespace ScreenTranslator.App.ViewModels;

public enum CaptureMode
{
    Single,
    Multiple,
}

public sealed class CaptureModeState : ObservableObject
{
    private CaptureMode _mode;

    public CaptureModeState(CaptureMode mode = CaptureMode.Single)
    {
        _mode = mode;
    }

    public CaptureMode Mode
    {
        get => _mode;
        private set
        {
            if (SetProperty(ref _mode, value))
            {
                OnPropertyChanged(nameof(InstructionText));
            }
        }
    }

    public string InstructionText => Mode == CaptureMode.Multiple
        ? "多条框选 · Tab 切换为单条 · Esc 或右键结束"
        : "单条框选 · Tab 切换为多条 · Esc 取消";

    public void Toggle() =>
        Mode = Mode == CaptureMode.Single
            ? CaptureMode.Multiple
            : CaptureMode.Single;
}
