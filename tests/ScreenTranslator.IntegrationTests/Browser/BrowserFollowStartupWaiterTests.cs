using ScreenTranslator.App.Services.Browser;

namespace ScreenTranslator.IntegrationTests.Browser;

public sealed class BrowserFollowStartupWaiterTests
{
    [Fact]
    public async Task WaitAsync_Retries_Until_Browser_Connection_Appears()
    {
        var attempts = 0;
        var waitingNotifications = 0;
        var waiter = new BrowserFollowStartupWaiter(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));

        var result = await waiter.WaitAsync<int>(
            _ => Task.FromResult<int?>(++attempts == 3 ? 42 : null),
            () => true,
            () => waitingNotifications++);

        Assert.Equal(42, result);
        Assert.Equal(3, attempts);
        Assert.Equal(2, waitingNotifications);
    }

    [Fact]
    public async Task WaitAsync_Stops_When_Overlay_Session_Is_No_Longer_Current()
    {
        var canContinue = true;
        var waiter = new BrowserFollowStartupWaiter(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));

        var result = await waiter.WaitAsync<int>(
            _ =>
            {
                canContinue = false;
                return Task.FromResult<int?>(null);
            },
            () => canContinue);

        Assert.Null(result);
    }
}
