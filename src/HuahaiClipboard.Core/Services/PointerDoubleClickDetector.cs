namespace HuahaiClipboard.Core.Services;

public sealed class PointerDoubleClickDetector(long maximumDelayMilliseconds, int maximumDeltaX, int maximumDeltaY)
{
    private long lastTimestamp = long.MinValue;
    private int lastX;
    private int lastY;

    public bool RegisterDown(long timestampMilliseconds, int x, int y)
    {
        var elapsed = timestampMilliseconds - lastTimestamp;
        var isDoubleClick =
            elapsed >= 0 &&
            elapsed <= maximumDelayMilliseconds &&
            Math.Abs(x - lastX) <= maximumDeltaX &&
            Math.Abs(y - lastY) <= maximumDeltaY;

        if (isDoubleClick)
        {
            lastTimestamp = long.MinValue;
            return true;
        }

        lastTimestamp = timestampMilliseconds;
        lastX = x;
        lastY = y;
        return false;
    }
}
