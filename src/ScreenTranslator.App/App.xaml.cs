using System.Windows;
using ScreenTranslator.App.Infrastructure;
using ScreenTranslator.App.Services;
using ScreenTranslator.App.Services.Browser;
using MessageBox = System.Windows.MessageBox;

namespace ScreenTranslator.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _singleInstanceGuard;

    public static ApplicationController Controller { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (NativeMessagingHost.IsBrowserInvocation(e.Args))
        {
            try
            {
                await NativeMessagingHost.RunAsync(
                    Console.OpenStandardInput(),
                    Console.OpenStandardOutput(),
                    CancellationToken.None);
                Shutdown();
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(
                    $"ScreenTranslator native messaging host failed: {exception.Message}");
                Shutdown(1);
            }

            return;
        }

        _singleInstanceGuard = new SingleInstanceGuard();
        if (!_singleInstanceGuard.IsPrimaryInstance)
        {
            MessageBox.Show(
                "屏译已经在运行，可从系统托盘打开设置。",
                "屏译",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                args.Handled = true;
                return;
            }

            MessageBox.Show(
                $"屏译遇到未处理的错误：{args.Exception.Message}",
                "屏译",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            Controller = new ApplicationController(this);
            await Controller.InitializeAsync();
            Controller.ShowSettings();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"屏译启动失败：{exception.Message}",
                "屏译",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Controller?.Dispose();
        _singleInstanceGuard?.Dispose();
        base.OnExit(e);
    }
}
