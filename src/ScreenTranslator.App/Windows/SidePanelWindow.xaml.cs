using System.Windows;
using System.Windows.Input;
using ScreenTranslator.App.ViewModels;
using Clipboard = System.Windows.Clipboard;

namespace ScreenTranslator.App.Windows;

public partial class SidePanelWindow : Window
{
    private const double PlacementGap = 12;
    private readonly TranslationResultViewModel _viewModel;

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
    }

    public TranslationResultViewModel ViewModel => _viewModel;

    public void PlaceBeside(Rect sourceBoundsDips, Rect workAreaDips)
    {
        double panelWidth = Math.Min(Width, workAreaDips.Width);
        MaxHeight = Math.Max(160, workAreaDips.Height * 0.7);
        double panelHeight = Math.Min(
            ActualHeight > 0 ? ActualHeight : 420,
            MaxHeight);

        double left;
        double top;

        if (sourceBoundsDips.Right + PlacementGap + panelWidth <= workAreaDips.Right)
        {
            left = sourceBoundsDips.Right + PlacementGap;
            top = sourceBoundsDips.Top;
        }
        else if (sourceBoundsDips.Left - PlacementGap - panelWidth >= workAreaDips.Left)
        {
            left = sourceBoundsDips.Left - PlacementGap - panelWidth;
            top = sourceBoundsDips.Top;
        }
        else if (sourceBoundsDips.Bottom + PlacementGap + panelHeight <= workAreaDips.Bottom)
        {
            left = sourceBoundsDips.Left;
            top = sourceBoundsDips.Bottom + PlacementGap;
        }
        else
        {
            left = sourceBoundsDips.Left;
            top = sourceBoundsDips.Top - PlacementGap - panelHeight;
        }

        Left = Math.Clamp(left, workAreaDips.Left, workAreaDips.Right - panelWidth);
        Top = Math.Clamp(top, workAreaDips.Top, workAreaDips.Bottom - panelHeight);
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

    private void Header_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
