using System.Windows;
using System.Windows.Media;
using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.App.ViewModels;
using ScreenTranslator.Core.Models;
using Brush = System.Windows.Media.Brush;
using Clipboard = System.Windows.Clipboard;
using WpfRect = System.Windows.Rect;

namespace ScreenTranslator.App.Windows;

public partial class TextOverlayWindow : Window, ITrackedOverlay
{
    private readonly TranslationResultViewModel _viewModel;
    private DipRect _trackingBounds;

    public TextOverlayWindow()
        : this(new TranslationResultViewModel())
    {
    }

    public TextOverlayWindow(TranslationResultViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.CopyRequested += ViewModel_OnCopyRequested;
        _viewModel.CloseRequested += ViewModel_OnCloseRequested;
    }

    public TranslationResultViewModel ViewModel => _viewModel;

    public DipRect TrackingBounds => _trackingBounds;

    public void SetBounds(WpfRect boundsDips)
    {
        Left = boundsDips.Left;
        Top = boundsDips.Top;
        Width = Math.Max(MinWidth, boundsDips.Width);
        Height = Math.Max(MinHeight, boundsDips.Height);
        _trackingBounds = new DipRect(
            boundsDips.Left,
            boundsDips.Top,
            boundsDips.Width,
            boundsDips.Height);
    }

    public void SetTextStyle(double fontSize, Brush? foreground = null, Brush? background = null)
    {
        TranslationText.FontSize = Math.Clamp(fontSize, 12, 32);
        if (foreground is not null)
        {
            TranslationText.Foreground = foreground;
        }

        if (background is not null)
        {
            RootCard.Background = background;
        }
    }

    public void SetInteractive(bool interactive)
    {
        RootCard.IsHitTestVisible = interactive;
        ActionBar.Visibility = interactive ? Visibility.Visible : Visibility.Collapsed;
    }

    public void MoveTo(DipRect bounds)
    {
        _trackingBounds = bounds;
        Left = bounds.X;
        Top = bounds.Y;
    }

    public void SetTrackingVisibility(bool visible)
    {
        RootCard.Visibility = visible
            ? Visibility.Visible
            : Visibility.Hidden;
        RootCard.IsHitTestVisible = visible && ActionBar.Visibility == Visibility.Visible;
    }

    protected override void OnClosed(EventArgs e)
    {
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
}
