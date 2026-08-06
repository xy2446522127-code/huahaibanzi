namespace HuahaiClipboard.Core.Services;

public sealed class DeferredActivationGate
{
    private readonly object synchronization = new();
    private bool ready;
    private bool pending;

    public bool RequestActivation()
    {
        lock (synchronization)
        {
            if (ready)
            {
                return true;
            }

            pending = true;
            return false;
        }
    }

    public bool MarkReady()
    {
        lock (synchronization)
        {
            ready = true;
            var shouldReplay = pending;
            pending = false;
            return shouldReplay;
        }
    }
}
