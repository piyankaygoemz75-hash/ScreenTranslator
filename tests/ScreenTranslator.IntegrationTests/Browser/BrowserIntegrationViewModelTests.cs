using ScreenTranslator.App.Services.Browser;
using ScreenTranslator.App.ViewModels;
using ScreenTranslator.Core.Browser;

namespace ScreenTranslator.IntegrationTests.Browser;

public sealed class BrowserIntegrationViewModelTests
{
    [Fact]
    public void Connected_Chrome_Shows_Ready_State()
    {
        var viewModel = new BrowserIntegrationViewModel();

        viewModel.UpdateConnection(BrowserKind.Chrome, connected: true);

        Assert.Equal("已连接", viewModel.ChromeStatus);
        Assert.True(viewModel.IsBrowserFollowingAvailable);
    }

    [Fact]
    public void Disabled_Following_Is_Not_Available_With_Connection()
    {
        var viewModel = new BrowserIntegrationViewModel();
        viewModel.UpdateConnection(BrowserKind.Edge, connected: true);

        viewModel.IsEnabled = false;

        Assert.False(viewModel.IsBrowserFollowingAvailable);
    }

    [Fact]
    public void Commands_Raise_Installation_Actions()
    {
        var viewModel = new BrowserIntegrationViewModel();
        BrowserKind? browser = null;
        var folderRequested = false;
        viewModel.OpenBrowserExtensionsRequested += (_, kind) => browser = kind;
        viewModel.OpenExtensionFolderRequested += (_, _) => folderRequested = true;

        viewModel.OpenEdgeExtensionsCommand.Execute(null);
        viewModel.OpenExtensionFolderCommand.Execute(null);

        Assert.Equal(BrowserKind.Edge, browser);
        Assert.True(folderRequested);
    }
}
