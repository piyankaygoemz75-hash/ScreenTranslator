using System.Drawing;
using Forms = System.Windows.Forms;

namespace ScreenTranslator.App.Services.Tray;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Icon? _applicationIcon;

    public TrayIconService()
    {
        _pauseItem = new Forms.ToolStripMenuItem("暂停快捷键");
        _pauseItem.Click += (_, _) =>
        {
            IsHotkeyPaused = !IsHotkeyPaused;
            _pauseItem.Text = IsHotkeyPaused ? "恢复快捷键" : "暂停快捷键";
            HotkeyPauseChanged?.Invoke(this, IsHotkeyPaused);
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("开始框选翻译", image: null, (_, _) => CaptureRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("显示或隐藏全部译文", image: null, (_, _) => ToggleOverlaysRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("设置", image: null, (_, _) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", image: null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _applicationIcon = Environment.ProcessPath is { } processPath
            ? Icon.ExtractAssociatedIcon(processPath)
            : null;
        _notifyIcon = new Forms.NotifyIcon
        {
            Text = "屏幕翻译",
            Icon = _applicationIcon ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? CaptureRequested;
    public event EventHandler? ToggleOverlaysRequested;
    public event EventHandler? ShowSettingsRequested;
    public event EventHandler<bool>? HotkeyPauseChanged;
    public event EventHandler? ExitRequested;

    public bool IsHotkeyPaused { get; private set; }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _applicationIcon?.Dispose();
    }
}
