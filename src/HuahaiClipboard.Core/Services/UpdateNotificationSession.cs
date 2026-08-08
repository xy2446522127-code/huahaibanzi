namespace HuahaiClipboard.Core.Services;

public sealed class UpdateNotificationSession
{
    private string? lastNotifiedVersion;

    public bool ShouldNotify(
        Version latestVersion,
        string? snoozedVersion,
        DateTimeOffset? snoozedUntil,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        return !string.Equals(
                lastNotifiedVersion,
                latestVersion.ToString(3),
                StringComparison.Ordinal) &&
            UpdateReminderPolicy.ShouldNotify(
                latestVersion,
                snoozedVersion,
                snoozedUntil,
                now);
    }

    public void MarkNotified(Version latestVersion)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        lastNotifiedVersion = latestVersion.ToString(3);
    }

    public void MarkSnoozed(Version latestVersion)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        if (string.Equals(
                lastNotifiedVersion,
                latestVersion.ToString(3),
                StringComparison.Ordinal))
        {
            lastNotifiedVersion = null;
        }
    }
}
