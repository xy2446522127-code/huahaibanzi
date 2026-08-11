namespace HuahaiClipboard.App.Infrastructure.Activation;

public sealed class ExternalActivationSignal : IDisposable
{
    public const string DefaultEventName = @"Local\HuahaiClipboard.Activate.v1";

    private readonly EventWaitHandle signal;
    private readonly RegisteredWaitHandle registration;
    private readonly Action activate;
    private int disposed;

    public ExternalActivationSignal(string eventName, Action activate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        this.activate = activate ?? throw new ArgumentNullException(nameof(activate));
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
        registration = ThreadPool.RegisterWaitForSingleObject(
            signal,
            static (state, timedOut) =>
            {
                if (!timedOut && state is ExternalActivationSignal owner &&
                    Volatile.Read(ref owner.disposed) == 0)
                {
                    owner.activate();
                }
            },
            this,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        registration.Unregister(null);
        signal.Dispose();
    }
}
