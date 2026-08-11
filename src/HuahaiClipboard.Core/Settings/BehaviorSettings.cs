namespace HuahaiClipboard.Core.Settings;

public sealed record BehaviorSettings(
    bool BackgroundEnabled,
    int AutoCleanupDays = 7,
    bool CheckUpdatesOnStartup = true,
    string? SnoozedUpdateVersion = null,
    DateTimeOffset? UpdateSnoozeUntil = null,
    bool HideOnOutsideClick = true)
{
    public static BehaviorSettings Default { get; } = new(true, 7);
}
