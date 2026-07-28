using System.Windows;
using ScreenTranslator.App.Pages;
using ScreenTranslator.App.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ScreenTranslator.App.Windows;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
        : this(App.Controller?.MainWindow ?? new MainWindowViewModel())
    {
    }

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true);
    }

    private void NavigationRoot_OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationRoot.Navigate(typeof(GeneralPage));
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemThemeWatcher.UnWatch(this);
        base.OnClosed(e);
    }
}
