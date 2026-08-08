namespace HuahaiClipboard.Core.Services;

public static class UpdateReminderPolicy
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan FirstFailureDelay = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RepeatedFailureDelay = TimeSpan.FromHours(1);
    public static readonly TimeSpan SnoozeDuration = TimeSpan.FromHours(24);

    public static bool ShouldNotify(
        Version latestVersion,
        string? snoozedVersion,
        DateTimeOffset? snoozedUntil,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        if (!Version.TryParse(snoozedVersion, out var snoozed) || snoozed != latestVersion)
        {
            return true;
        }

        return snoozedUntil is null || snoozedUntil <= now;
    }

    public static TimeSpan DelayAfterFailure(int consecutiveFailures) =>
        consecutiveFailures <= 1 ? FirstFailureDelay : RepeatedFailureDelay;
}
