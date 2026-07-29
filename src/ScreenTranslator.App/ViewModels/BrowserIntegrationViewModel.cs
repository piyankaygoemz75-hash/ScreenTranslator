using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.Core.Browser;

namespace ScreenTranslator.App.ViewModels;

public sealed class BrowserIntegrationViewModel : ObservableObject
{
    private string _chromeStatus = "未连接";
    private string _edgeStatus = "未连接";
    private string _detailText = "安装配套扩展后，普通网页中的原位译文可随滚动移动。";
    private bool _isEnabled = true;

    public BrowserIntegrationViewModel()
    {
        OpenChromeExtensionsCommand = new RelayCommand(
            () => OpenBrowserExtensionsRequested?.Invoke(this, BrowserKind.Chrome));
        OpenEdgeExtensionsCommand = new RelayCommand(
            () => OpenBrowserExtensionsRequested?.Invoke(this, BrowserKind.Edge));
        OpenExtensionFolderCommand = new RelayCommand(
            () => OpenExtensionFolderRequested?.Invoke(this, EventArgs.Empty));
    }

    public string ChromeStatus
    {
        get => _chromeStatus;
        private set => SetProperty(ref _chromeStatus, value);
    }

    public string EdgeStatus
    {
        get => _edgeStatus;
        private set => SetProperty(ref _edgeStatus, value);
    }

    public string DetailText
    {
        get => _detailText;
        set => SetProperty(ref _detailText, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(IsBrowserFollowingAvailable));
            }
        }
    }

    public bool IsBrowserFollowingAvailable =>
        IsEnabled &&
        (ChromeStatus == "已连接" || EdgeStatus == "已连接");

    public IRelayCommand OpenChromeExtensionsCommand { get; }

    public IRelayCommand OpenEdgeExtensionsCommand { get; }

    public IRelayCommand OpenExtensionFolderCommand { get; }

    public event EventHandler<BrowserKind>? OpenBrowserExtensionsRequested;

    public event EventHandler? OpenExtensionFolderRequested;

    public void UpdateConnection(BrowserKind browser, bool connected)
    {
        var status = connected ? "已连接" : "未连接";
        if (browser == BrowserKind.Chrome)
        {
            ChromeStatus = status;
        }
        else
        {
            EdgeStatus = status;
        }

        OnPropertyChanged(nameof(IsBrowserFollowingAvailable));
    }
}
