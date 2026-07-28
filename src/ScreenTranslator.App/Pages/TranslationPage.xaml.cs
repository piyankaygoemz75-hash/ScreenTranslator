using System.Windows;
using System.Windows.Controls;
using ScreenTranslator.App.ViewModels;

namespace ScreenTranslator.App.Pages;

public partial class TranslationPage : Page
{
    public TranslationPage()
        : this(App.Controller?.TranslationSettings ?? new TranslationSettingsViewModel())
    {
    }

    public TranslationPage(TranslationSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is TranslationSettingsViewModel viewModel &&
            sender is PasswordBox passwordBox)
        {
            viewModel.ApiKey = passwordBox.Password;
        }
    }
}
