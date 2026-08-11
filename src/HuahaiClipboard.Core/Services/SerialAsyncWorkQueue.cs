namespace HuahaiClipboard.Core.Services;

public sealed class SerialAsyncWorkQueue
{
    private readonly object sync = new();
    private Task tail = Task.CompletedTask;

    public Task EnqueueAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (sync)
        {
            var execution = ExecuteAfterAsync(tail, operation);
            tail = ObserveCompletionAsync(execution);
            return execution;
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        Task snapshot;
        lock (sync)
        {
            snapshot = tail;
        }

        return cancellationToken.CanBeCanceled
            ? snapshot.WaitAsync(cancellationToken)
            : snapshot;
    }

    private static async Task ExecuteAfterAsync(Task predecessor, Func<Task> operation)
    {
        await Task.Yield();
        await predecessor.ConfigureAwait(false);
        await operation().ConfigureAwait(false);
    }

    private static async Task ObserveCompletionAsync(Task operation)
    {
        try
        {
            await operation.ConfigureAwait(false);
        }
        catch
        {
            // Individual callers observe their own failure; later registered work must still run.
        }
    }
}
