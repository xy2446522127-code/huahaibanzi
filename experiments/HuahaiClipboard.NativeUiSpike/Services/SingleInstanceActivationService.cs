namespace HuahaiClipboard.NativeUiSpike.Services;

public sealed class SingleInstanceActivationService : IDisposable
{
    public const string ActivationName = "Local\\HuahaiClipboard.NativeUiSpike.Activate";
    public const string MutexName = "Local\\HuahaiClipboard.NativeUiSpike.Mutex";

    private readonly EventWaitHandle? activationEvent;
    private readonly Mutex? mutex;
    private RegisteredWaitHandle? registeredWait;
    private bool ownsMutex;

    private SingleInstanceActivationService(Mutex? mutex, EventWaitHandle? activationEvent, bool isPrimary)
    {
        this.mutex = mutex;
        this.activationEvent = activationEvent;
        IsPrimary = isPrimary;
    }

    public event EventHandler? Activated;

    public bool IsPrimary { get; }

    public void StartListening()
    {
        if (!IsPrimary || activationEvent is null || registeredWait is not null) return;
        registeredWait = ThreadPool.RegisterWaitForSingleObject(
            activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut) Activated?.Invoke(this, EventArgs.Empty);
            },
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public static SingleInstanceActivationService CreateOrSignal()
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool ownsMutex;
        try
        {
            ownsMutex = mutex.WaitOne(0, exitContext: false);
        }
        catch (AbandonedMutexException)
        {
            ownsMutex = true;
        }

        if (!ownsMutex)
        {
            mutex.Dispose();
            SignalPrimary();
            return new SingleInstanceActivationService(null, null, isPrimary: false);
        }

        var activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationName);
        var service = new SingleInstanceActivationService(mutex, activationEvent, isPrimary: true)
        {
            ownsMutex = true,
        };
        return service;
    }

    public void Dispose()
    {
        registeredWait?.Unregister(null);
        registeredWait = null;
        activationEvent?.Dispose();
        if (ownsMutex)
        {
            try
            {
                mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            ownsMutex = false;
        }

        mutex?.Dispose();
    }

    private static void SignalPrimary()
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                using var existing = EventWaitHandle.OpenExisting(ActivationName);
                existing.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 39)
            {
                Thread.Sleep(25);
            }
        }
    }
}
