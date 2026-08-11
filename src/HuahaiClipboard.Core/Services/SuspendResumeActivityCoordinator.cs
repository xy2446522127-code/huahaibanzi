namespace HuahaiClipboard.Core.Services;

public sealed class SuspendResumeActivityCoordinator
{
    private readonly SerialAsyncWorkQueue suspendQueue = new();
    private long activityVersion;

    public Task RequestSuspendAsync(Func<long, Task> suspend)
    {
        ArgumentNullException.ThrowIfNull(suspend);
        var version = Interlocked.Increment(ref activityVersion);
        return suspendQueue.EnqueueAsync(() => suspend(version));
    }

    public void MarkActive() => Interlocked.Increment(ref activityVersion);

    public bool IsCurrent(long version) =>
        version == Interlocked.Read(ref activityVersion);

    public Task WaitForPendingSuspendAsync(CancellationToken cancellationToken = default) =>
        suspendQueue.FlushAsync(cancellationToken);
}
