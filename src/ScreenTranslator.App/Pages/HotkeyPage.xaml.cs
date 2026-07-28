using System.Windows.Controls;
using ScreenTranslator.App.ViewModels;

namespace ScreenTranslator.App.Pages;

public partial class HotkeyPage : Page
{
    public HotkeyPage()
        : this(App.Controller?.HotkeySettings ?? new HotkeySettingsViewModel())
    {
    }

    public HotkeyPage(HotkeySettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
