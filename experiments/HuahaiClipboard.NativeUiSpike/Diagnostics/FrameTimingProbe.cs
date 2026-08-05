namespace HuahaiClipboard.NativeUiSpike.Diagnostics;

public readonly record struct FrameTimingSummary(
    int Count,
    double MeanIntervalMs,
    double P95IntervalMs,
    double DerivedFps);

public static class FrameTimingProbe
{
    public static FrameTimingSummary Summarize(IReadOnlyCollection<double> intervals)
    {
        if (intervals.Count == 0) return new FrameTimingSummary(0, 0, 0, 0);

        var ordered = intervals.OrderBy(value => value).ToArray();
        var mean = ordered.Average();
        var percentileIndex = Math.Clamp((int)Math.Ceiling(ordered.Length * 0.95) - 1, 0, ordered.Length - 1);
        return new FrameTimingSummary(
            ordered.Length,
            Math.Round(mean, 3),
            Math.Round(ordered[percentileIndex], 3),
            Math.Round(1000d / mean, 3));
    }
}
