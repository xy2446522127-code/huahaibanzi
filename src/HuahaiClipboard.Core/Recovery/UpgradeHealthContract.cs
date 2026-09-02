namespace HuahaiClipboard.Core.Recovery;

public sealed class UpgradeDataManifest
{
    public UpgradeDataManifest(
        IEnumerable<string> historyIds,
        IEnumerable<string> todoIds,
        IEnumerable<string> noteIds,
        IEnumerable<string> attachmentHashes)
    {
        HistoryIds = Normalize(historyIds);
        TodoIds = Normalize(todoIds);
        NoteIds = Normalize(noteIds);
        AttachmentHashes = Normalize(attachmentHashes);
    }

    public IReadOnlySet<string> HistoryIds { get; }

    public IReadOnlySet<string> TodoIds { get; }

    public IReadOnlySet<string> NoteIds { get; }

    public IReadOnlySet<string> AttachmentHashes { get; }

    public bool IsSupersetOf(UpgradeDataManifest baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return HistoryIds.IsSupersetOf(baseline.HistoryIds) &&
               TodoIds.IsSupersetOf(baseline.TodoIds) &&
               NoteIds.IsSupersetOf(baseline.NoteIds) &&
               AttachmentHashes.IsSupersetOf(baseline.AttachmentHashes);
    }

    private static IReadOnlySet<string> Normalize(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
