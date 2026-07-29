using System.Diagnostics;

namespace ScreenTranslator.App.Services.Browser;

public sealed class BrowserFollowStartupWaiter
{
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _retryInterval;

    public BrowserFollowStartupWaiter(
        TimeSpan timeout,
        TimeSpan retryInterval)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (retryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(retryInterval));
        }

        _timeout = timeout;
        _retryInterval = retryInterval;
    }

    public async Task<T?> WaitAsync<T>(
        Func<CancellationToken, Task<T?>> probe,
        Func<bool> canContinue,
        Action? onWaiting = null,
        CancellationToken cancellationToken = default)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(canContinue);

        var startedAt = Stopwatch.GetTimestamp();
        while (canContinue())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await probe(cancellationToken);
            if (result is not null)
            {
                return result;
            }

            var elapsed = Stopwatch.GetElapsedTime(startedAt);
            if (elapsed >= _timeout)
            {
                return null;
            }

            onWaiting?.Invoke();
            var delay = _timeout - elapsed;
            if (delay > _retryInterval)
            {
                delay = _retryInterval;
            }

            await Task.Delay(delay, cancellationToken);
        }

        return null;
    }
}
