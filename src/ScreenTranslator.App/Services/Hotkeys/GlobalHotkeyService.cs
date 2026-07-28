using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenTranslator.App.Services.Hotkeys;

public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler? CaptureRequested;

    bool IsRegistered { get; }

    void Register(ModifierKeys modifiers, Key key);

    void Unregister();
}

public sealed class HotkeyConflictException : Exception
{
    public HotkeyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyId = 0x5343;
    private const int WmHotkey = 0x0312;
    private static readonly IntPtr MessageOnlyWindow = new(-3);

    private readonly HwndSource _messageSource;
    private bool _disposed;

    public GlobalHotkeyService()
    {
        var parameters = new HwndSourceParameters("ScreenTranslator.Hotkey")
        {
            ParentWindow = MessageOnlyWindow,
            WindowStyle = 0,
            Width = 0,
            Height = 0
        };

        _messageSource = new HwndSource(parameters);
        _messageSource.AddHook(WindowProcedure);
    }

    public event EventHandler? CaptureRequested;

    public bool IsRegistered { get; private set; }

    public void Register(ModifierKeys modifiers, Key key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Unregister();

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (!RegisterHotKey(_messageSource.Handle, HotkeyId, ToNativeModifiers(modifiers), (uint)virtualKey))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            throw new HotkeyConflictException("快捷键已被其他程序占用，请在设置中更换。", error);
        }

        IsRegistered = true;
    }

    public void Unregister()
    {
        if (!IsRegistered)
        {
            return;
        }

        _ = UnregisterHotKey(_messageSource.Handle, HotkeyId);
        IsRegistered = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _messageSource.RemoveHook(WindowProcedure);
        _messageSource.Dispose();
        _disposed = true;
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            CaptureRequested?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= 0x0001;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= 0x0002;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= 0x0004;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= 0x0008;
        }

        return result | 0x4000;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);
}
