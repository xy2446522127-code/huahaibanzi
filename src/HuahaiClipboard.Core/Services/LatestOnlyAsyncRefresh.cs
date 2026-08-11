namespace HuahaiClipboard.Core.Services;

public sealed class LatestOnlyAsyncRefresh(Func<CancellationToken, Task> refresh)
{
    private readonly object sync = new();
    private readonly Func<CancellationToken, Task> refresh =
        refresh ?? throw new ArgumentNullException(nameof(refresh));
    private long requestedRevision;
    private long completedRevision;
    private Task? worker;

    public Task RequestAsync(CancellationToken cancellationToken = default)
    {
        Task pending;
        lock (sync)
        {
            requestedRevision++;
            worker ??= RunAsync();
            pending = worker;
        }

        return cancellationToken.CanBeCanceled
            ? pending.WaitAsync(cancellationToken)
            : pending;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            Task pending;
            lock (sync)
            {
                if (completedRevision >= requestedRevision)
                {
                    return;
                }

                worker ??= RunAsync();
                pending = worker;
            }

            await pending.WaitAsync(cancellationToken);
        }
    }

    private async Task RunAsync()
    {
        await Task.Yield();
        while (true)
        {
            long targetRevision;
            lock (sync)
            {
                targetRevision = requestedRevision;
            }

            try
            {
                await refresh(CancellationToken.None);
            }
            catch
            {
                var retryForNewerRequest = false;
                lock (sync)
                {
                    retryForNewerRequest = requestedRevision > targetRevision;
                    if (!retryForNewerRequest)
                    {
                        worker = null;
                    }
                }

                if (retryForNewerRequest)
                {
                    continue;
                }

                throw;
            }

            lock (sync)
            {
                completedRevision = targetRevision;
                if (completedRevision >= requestedRevision)
                {
                    worker = null;
                    return;
                }
            }
        }
    }
}
