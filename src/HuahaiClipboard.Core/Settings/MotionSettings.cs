namespace HuahaiClipboard.Core.Settings;

public enum PetalLevel
{
    Off,
    Low,
    Medium,
    High
}

public sealed record MotionSettings(
    PetalLevel PetalLevel,
    bool ReduceMotion,
    int ClickDurationMs = 760,
    int ReducedClickDurationMs = 120);
