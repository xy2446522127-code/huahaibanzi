namespace HuahaiClipboard.Core.Services;

public static class UpdateCacheLayout
{
    public static string ResolvePendingDirectory(string temporaryRoot, string userKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(userKey);
        return Path.Combine(
            Path.GetFullPath(temporaryRoot),
            "HuahaiClipboard",
            "Updates",
            userKey,
            "Pending");
    }
}
