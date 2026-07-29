using ScreenTranslator.App.ViewModels;
using ScreenTranslator.App.Windows;
using ScreenTranslator.IntegrationTests.TestInfrastructure;

namespace ScreenTranslator.IntegrationTests.Windows;

public sealed class MainWindowLifecycleTests
{
    [Fact]
    public async Task Close_Hides_To_Tray_And_Shutdown_Closes_Window()
    {
        await StaTest.RunAsync(() =>
        {
            var application = new ScreenTranslator.App.App();
            application.InitializeComponent();
            var window = new MainWindow(new MainWindowViewModel())
            {
                HideOnClose = true,
            };
            var wasClosed = false;
            window.Closed += (_, _) => wasClosed = true;

            window.Show();
            window.Close();

            Assert.True(window.IsLoaded);
            Assert.False(window.IsVisible);
            Assert.False(wasClosed);

            window.IsApplicationShuttingDown = true;
            window.Close();

            Assert.True(wasClosed);
            application.Shutdown();
        });
    }
}
