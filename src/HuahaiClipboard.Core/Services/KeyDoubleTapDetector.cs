namespace HuahaiClipboard.Core.Services;

public sealed class KeyDoubleTapDetector(uint thresholdMilliseconds = 300)
{
    private readonly uint thresholdMilliseconds = thresholdMilliseconds;
    private uint lastKey;
    private uint lastTimestamp;
    private bool armed;

    public bool RegisterDown(uint virtualKey, uint timestamp)
    {
        if (!armed)
        {
            lastKey = virtualKey;
            lastTimestamp = timestamp;
            armed = true;
            return false;
        }

        var elapsed = unchecked(timestamp - lastTimestamp);
        var matched = virtualKey == lastKey && elapsed <= thresholdMilliseconds;
        lastKey = virtualKey;
        lastTimestamp = timestamp;
        armed = !matched;
        return matched;
    }

    public void Reset() => armed = false;
}
