namespace HuahaiClipboard.Core.Services;

public static class PanelScalePolicy
{
    public const int MinimumPercent = 80;
    public const int MaximumPercent = 160;
    public const int DefaultPercent = 100;

    public static int NormalizePercent(double ratio)
    {
        if (!double.IsFinite(ratio)) return DefaultPercent;
        return Math.Clamp(
            (int)Math.Round(ratio * 100d, MidpointRounding.AwayFromZero),
            MinimumPercent,
            MaximumPercent);
    }

    public static double ToRatio(int percent) =>
        Math.Clamp(percent, MinimumPercent, MaximumPercent) / 100d;

    public static double NormalizeRatio(double ratio) => ToRatio(NormalizePercent(ratio));
}

public sealed class PanelScalePreviewSession
{
    public PanelScalePreviewSession(double committedRatio)
    {
        CommittedRatio = PanelScalePolicy.NormalizeRatio(committedRatio);
        CurrentRatio = CommittedRatio;
    }

    public double CommittedRatio { get; private set; }

    public double CurrentRatio { get; private set; }

    public double Preview(double ratio) =>
        CurrentRatio = PanelScalePolicy.NormalizeRatio(ratio);

    public double Commit(double ratio)
    {
        CommittedRatio = PanelScalePolicy.NormalizeRatio(ratio);
        return CurrentRatio = CommittedRatio;
    }

    public double Cancel() => CurrentRatio = CommittedRatio;
}
