namespace HuahaiClipboard.Core.Services;

public sealed class UpdateStartupGate
{
    private int started;

    public bool TryBegin(bool shellReady, bool trayReady)
    {
        if (!shellReady || !trayReady)
        {
            return false;
        }

        return Interlocked.CompareExchange(ref started, 1, 0) == 0;
    }
}
