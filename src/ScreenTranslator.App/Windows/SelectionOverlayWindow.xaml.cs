using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScreenTranslator.App.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;
using Size = System.Windows.Size;

namespace ScreenTranslator.App.Windows;

public partial class SelectionOverlayWindow : Window
{
    private readonly SelectionOverlayViewModel _viewModel;
    private Point _monitorOriginPhysical;
    private double _dpiScaleX = 1;
    private double _dpiScaleY = 1;

    public SelectionOverlayWindow()
        : this(new SelectionOverlayViewModel())
    {
    }

    public SelectionOverlayWindow(SelectionOverlayViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => UpdateSelectionVisual();
    }

    public event EventHandler<ScreenSelectionCompletedEventArgs>? SelectionCompleted;

    public event EventHandler? SelectionCancelled;

    public void Configure(
        ImageSource screenshot,
        Rect monitorBoundsDips,
        Point monitorOriginPhysical,
        double dpiScaleX,
        double dpiScaleY)
    {
        _viewModel.Screenshot = screenshot;
        _monitorOriginPhysical = monitorOriginPhysical;
        _dpiScaleX = Math.Max(dpiScaleX, 0.01);
        _dpiScaleY = Math.Max(dpiScaleY, 0.01);

        Left = monitorBoundsDips.Left;
        Top = monitorBoundsDips.Top;
        Width = monitorBoundsDips.Width;
        Height = monitorBoundsDips.Height;
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        Activate();
        Focus();
    }

    private void Window_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CaptureMouse();
        _viewModel.BeginSelection(ClampToWindow(e.GetPosition(this)));
        UpdateSelectionVisual();
    }

    private void Window_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_viewModel.IsSelecting || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        _viewModel.UpdateSelection(ClampToWindow(e.GetPosition(this)));
        UpdateSelectionVisual();
    }

    private void Window_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsSelecting)
        {
            return;
        }

        ReleaseMouseCapture();
        Rect selection = _viewModel.CompleteSelection(ClampToWindow(e.GetPosition(this)));
        UpdateSelectionVisual();

        Int32Rect physical = ToPhysicalPixels(selection);
        if (physical.Width < 8 || physical.Height < 8)
        {
            CancelSelection();
            return;
        }

        SelectionCompleted?.Invoke(
            this,
            new ScreenSelectionCompletedEventArgs(selection, physical));
        Close();
    }

    private void Window_OnMouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        CancelSelection();

    private void Window_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelSelection();
        }
    }

    private void CancelSelection()
    {
        _viewModel.Reset();
        SelectionCancelled?.Invoke(this, EventArgs.Empty);
        Close();
    }

    private Point ClampToWindow(Point point) =>
        new(
            Math.Clamp(point.X, 0, ActualWidth),
            Math.Clamp(point.Y, 0, ActualHeight));

    private Int32Rect ToPhysicalPixels(Rect selection) =>
        new(
            (int)Math.Round(_monitorOriginPhysical.X + selection.X * _dpiScaleX),
            (int)Math.Round(_monitorOriginPhysical.Y + selection.Y * _dpiScaleY),
            (int)Math.Round(selection.Width * _dpiScaleX),
            (int)Math.Round(selection.Height * _dpiScaleY));

    private void UpdateSelectionVisual()
    {
        double width = Math.Max(ActualWidth, 0);
        double height = Math.Max(ActualHeight, 0);
        Rect selection = _viewModel.Selection;

        if (!_viewModel.HasSelection)
        {
            SetRectangle(DimTop, 0, 0, width, height);
            SetRectangle(DimLeft, 0, 0, 0, 0);
            SetRectangle(DimRight, 0, 0, 0, 0);
            SetRectangle(DimBottom, 0, 0, 0, 0);
            SelectionBorder.Visibility = Visibility.Collapsed;
            SizeBadge.Visibility = Visibility.Collapsed;
            return;
        }

        SetRectangle(DimTop, 0, 0, width, selection.Top);
        SetRectangle(DimLeft, 0, selection.Top, selection.Left, selection.Height);
        SetRectangle(
            DimRight,
            selection.Right,
            selection.Top,
            Math.Max(0, width - selection.Right),
            selection.Height);
        SetRectangle(
            DimBottom,
            0,
            selection.Bottom,
            width,
            Math.Max(0, height - selection.Bottom));

        SelectionBorder.Visibility = Visibility.Visible;
        Canvas.SetLeft(SelectionBorder, selection.Left);
        Canvas.SetTop(SelectionBorder, selection.Top);
        SelectionBorder.Width = selection.Width;
        SelectionBorder.Height = selection.Height;

        SizeBadge.Visibility = Visibility.Visible;
        SizeBadge.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double badgeLeft = Math.Clamp(
            selection.Left,
            8,
            Math.Max(8, width - SizeBadge.DesiredSize.Width - 8));
        double badgeTop = selection.Bottom + SizeBadge.DesiredSize.Height + 8 <= height
            ? selection.Bottom + 8
            : Math.Max(8, selection.Top - SizeBadge.DesiredSize.Height - 8);
        Canvas.SetLeft(SizeBadge, badgeLeft);
        Canvas.SetTop(SizeBadge, badgeTop);
    }

    private static void SetRectangle(
        Rectangle rectangle,
        double left,
        double top,
        double width,
        double height)
    {
        Canvas.SetLeft(rectangle, left);
        Canvas.SetTop(rectangle, top);
        rectangle.Width = Math.Max(width, 0);
        rectangle.Height = Math.Max(height, 0);
    }
}

public sealed class ScreenSelectionCompletedEventArgs(
    Rect boundsInWindowDips,
    Int32Rect boundsInPhysicalPixels) : EventArgs
{
    public Rect BoundsInWindowDips { get; } = boundsInWindowDips;

    public Int32Rect BoundsInPhysicalPixels { get; } = boundsInPhysicalPixels;
}
