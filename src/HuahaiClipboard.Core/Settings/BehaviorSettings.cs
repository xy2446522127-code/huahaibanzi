namespace HuahaiClipboard.Core.Settings;

public sealed record BehaviorSettings(
    bool BackgroundEnabled,
    int AutoCleanupDays = 7,
    bool CheckUpdatesOnStartup = true,
    string? SnoozedUpdateVersion = null,
    DateTimeOffset? UpdateSnoozeUntil = null,
    bool HideOnOutsideClick = true,
    bool AutoCleanupCountEnabled = false,
    int AutoCleanupCount = 100)
{
    public static BehaviorSettings Default { get; } = new(true, 7);
}
