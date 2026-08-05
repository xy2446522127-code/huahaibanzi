namespace HuahaiClipboard.Core.Settings;

public sealed record BehaviorSettings(
    bool BackgroundEnabled,
    int AutoCleanupDays = 7,
    bool CheckUpdatesOnStartup = true)
{
    public static BehaviorSettings Default { get; } = new(true, 7);
}
