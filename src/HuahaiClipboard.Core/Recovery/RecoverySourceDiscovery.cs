namespace HuahaiClipboard.Core.Recovery;

public static class RecoverySourceDiscovery
{
    public static IReadOnlyList<RecoverySource> Discover(IEnumerable<RecoveryDiscoveryHint> hints)
    {
        ArgumentNullException.ThrowIfNull(hints);
        var sources = new Dictionary<string, RecoverySource>(StringComparer.OrdinalIgnoreCase);
        foreach (var hint in hints)
        {
            if (hint is null || string.IsNullOrWhiteSpace(hint.Root)) continue;
            try
            {
                var root = Path.GetFullPath(hint.Root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (!Directory.Exists(root) ||
                    (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                sources.TryAdd(root, new RecoverySource(root, hint.Kind, hint.Provenance ?? string.Empty));
            }
            catch (IOException)
            {
                // One unavailable candidate must not stop recovery discovery.
            }
            catch (UnauthorizedAccessException)
            {
                // Discovery is best-effort and must not elevate privileges.
            }
            catch (ArgumentException)
            {
                // Ignore malformed paths supplied by stale metadata.
            }
        }

        return sources.Values
            .OrderBy(source => source.Root, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
