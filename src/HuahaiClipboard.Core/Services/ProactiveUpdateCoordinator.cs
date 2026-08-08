namespace HuahaiClipboard.Core.Services;

public sealed class ProactiveUpdateCoordinator : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<bool>> isEnabledAsync;
    private readonly Func<CancellationToken, Task<UpdateCheckResult>> checkAsync;
    private readonly Func<UpdateCheckResult, CancellationToken, Task> onResultAsync;
    private readonly Func<Exception, CancellationToken, Task> onErrorAsync;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private readonly CancellationTokenSource cancellation = new();
    private readonly object sync = new();
    private Task? loopTask;

    public ProactiveUpdateCoordinator(
        Func<CancellationToken, Task<bool>> isEnabledAsync,
        Func<CancellationToken, Task<UpdateCheckResult>> checkAsync,
        Func<UpdateCheckResult, CancellationToken, Task> onResultAsync,
        Func<Exception, CancellationToken, Task>? onErrorAsync = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        this.isEnabledAsync = isEnabledAsync ?? throw new ArgumentNullException(nameof(isEnabledAsync));
        this.checkAsync = checkAsync ?? throw new ArgumentNullException(nameof(checkAsync));
        this.onResultAsync = onResultAsync ?? throw new ArgumentNullException(nameof(onResultAsync));
        this.onErrorAsync = onErrorAsync ?? ((_, _) => Task.CompletedTask);
        this.delayAsync = delayAsync ?? Task.Delay;
    }

    public void Start()
    {
        lock (sync)
        {
            loopTask ??= RunAsync(cancellation.Token);
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var consecutiveFailures = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var nextDelay = UpdateReminderPolicy.PollInterval;
                try
                {
                    if (await isEnabledAsync(cancellationToken).ConfigureAwait(false))
                    {
                        var result = await checkAsync(cancellationToken).ConfigureAwait(false);
                        await onResultAsync(result, cancellationToken).ConfigureAwait(false);
                        consecutiveFailures = 0;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    consecutiveFailures++;
                    nextDelay = UpdateReminderPolicy.DelayAfterFailure(consecutiveFailures);
                    await onErrorAsync(exception, cancellationToken).ConfigureAwait(false);
                }

                await delayAsync(nextDelay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        cancellation.Cancel();
        Task? running;
        lock (sync)
        {
            running = loopTask;
        }

        if (running is not null)
        {
            try
            {
                await running.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        cancellation.Dispose();
    }
}
