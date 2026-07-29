using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ScreenTranslator.App.ViewModels;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Models;
using ScreenTranslator.Core.Settings;
using Clipboard = System.Windows.Clipboard;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace ScreenTranslator.App.Windows;

public partial class SidePanelWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmNcLButtonDown = 0x00A1;
    private const int WmNcHitTest = 0x0084;
    private const int WmExitSizeMove = 0x0232;
    private const int HtCaption = 2;
    private const int ResizeBorderPixels = 8;

    private readonly TranslationResultViewModel _viewModel;
    private readonly DispatcherTimer _placementTimer;
    private HwndSource? _source;
    private DipRect _workArea = new(0, 0, 1920, 1080);
    private bool _isApplyingBounds;

    public SidePanelWindow()
        : this(new TranslationResultViewModel())
    {
    }

    public SidePanelWindow(TranslationResultViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CopyRequested += ViewModel_OnCopyRequested;
        _viewModel.CloseRequested += ViewModel_OnCloseRequested;
        _placementTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350),
        };
        _placementTimer.Tick += PlacementTimer_OnTick;
        SourceInitialized += Window_OnSourceInitialized;
        LocationChanged += Window_OnPlacementChanged;
        SizeChanged += Window_OnPlacementChanged;
    }

    public TranslationResultViewModel ViewModel => _viewModel;

    public event EventHandler<WindowPlacement>? PlacementChanged;

    public void ApplyPlacement(DipRect bounds, DipRect workArea)
    {
        _workArea = workArea;
        MaxHeight = Math.Max(MinHeight, workArea.Height * 0.8);
        ApplyBounds(bounds);
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_placementTimer.IsEnabled)
        {
            ClampAndPublishPlacement();
        }

        _placementTimer.Stop();
        _placementTimer.Tick -= PlacementTimer_OnTick;
        LocationChanged -= Window_OnPlacementChanged;
        SizeChanged -= Window_OnPlacementChanged;
        SourceInitialized -= Window_OnSourceInitialized;
        _source?.RemoveHook(WindowProcedure);
        _viewModel.CopyRequested -= ViewModel_OnCopyRequested;
        _viewModel.CloseRequested -= ViewModel_OnCloseRequested;
        base.OnClosed(e);
    }

    private static void ViewModel_OnCopyRequested(object? sender, string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
        }
    }

    private void ViewModel_OnCloseRequested(object? sender, EventArgs e) => Close();

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _ = ReleaseCapture();
        _ = SendMessage(
            new WindowInteropHelper(this).Handle,
            WmNcLButtonDown,
            new IntPtr(HtCaption),
            IntPtr.Zero);
    }

    private void Window_OnSourceInitialized(object? sender, EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WindowProcedure);

        var extendedStyle = GetWindowLongPtr(helper.Handle, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(
            helper.Handle,
            GwlExStyle,
            new IntPtr(extendedStyle | WsExToolWindow | WsExNoActivate));
    }

    private void Window_OnPlacementChanged(object? sender, EventArgs e)
    {
        if (_isApplyingBounds)
        {
            return;
        }

        _placementTimer.Stop();
        _placementTimer.Start();
    }

    private void PlacementTimer_OnTick(object? sender, EventArgs e)
    {
        _placementTimer.Stop();
        ClampAndPublishPlacement();
    }

    private void ClampAndPublishPlacement()
    {
        _placementTimer.Stop();
        var candidate = new DipRect(
            Left,
            Top,
            ActualWidth > 0 ? ActualWidth : Width,
            ActualHeight > 0 ? ActualHeight : Height);
        var clamped = SidePanelBoundsService.Clamp(candidate, _workArea);
        ApplyBounds(clamped);
        PlacementChanged?.Invoke(
            this,
            new WindowPlacement(
                clamped.X,
                clamped.Y,
                clamped.Width,
                clamped.Height));
    }

    private void ApplyBounds(DipRect bounds)
    {
        _isApplyingBounds = true;
        try
        {
            Left = bounds.X;
            Top = bounds.Y;
            Width = bounds.Width;
            Height = bounds.Height;
        }
        finally
        {
            _isApplyingBounds = false;
        }
    }

    private IntPtr WindowProcedure(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmExitSizeMove)
        {
            Dispatcher.BeginInvoke(ClampAndPublishPlacement);
            return IntPtr.Zero;
        }

        if (message != WmNcHitTest || !GetWindowRect(hwnd, out var rect))
        {
            return IntPtr.Zero;
        }

        var x = unchecked((short)(long)lParam);
        var y = unchecked((short)((long)lParam >> 16));
        var left = x >= rect.Left && x < rect.Left + ResizeBorderPixels;
        var right = x <= rect.Right && x > rect.Right - ResizeBorderPixels;
        var top = y >= rect.Top && y < rect.Top + ResizeBorderPixels;
        var bottom = y <= rect.Bottom && y > rect.Bottom - ResizeBorderPixels;
        var hit = (left, right, top, bottom) switch
        {
            (true, _, true, _) => 13,
            (_, true, true, _) => 14,
            (true, _, _, true) => 16,
            (_, true, _, true) => 17,
            (true, _, _, _) => 10,
            (_, true, _, _) => 11,
            (_, _, true, _) => 12,
            (_, _, _, true) => 15,
            _ => 0,
        };

        if (hit != 0)
        {
            handled = true;
            return new IntPtr(hit);
        }

        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rect);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern IntPtr GetWindowLong32(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr windowHandle,
        int index,
        IntPtr newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(
        IntPtr windowHandle,
        int index,
        IntPtr newValue);

    private static IntPtr GetWindowLongPtr(IntPtr windowHandle, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(windowHandle, index)
            : GetWindowLong32(windowHandle, index);

    private static IntPtr SetWindowLongPtr(
        IntPtr windowHandle,
        int index,
        IntPtr newValue) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(windowHandle, index, newValue)
            : SetWindowLong32(windowHandle, index, newValue);
}
