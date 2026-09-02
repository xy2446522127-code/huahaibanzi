namespace HuahaiClipboard.Core.Recovery;

using System.Text.Json;
using HuahaiClipboard.Core.Services;

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

public sealed record UpgradeHealthReceipt(
    string CandidateToken,
    UpgradeDataManifest PostUpgradeManifest,
    DateTimeOffset VerifiedAt)
{
    public bool Verifies(string expectedToken, UpgradeDataManifest baseline)
    {
        return !string.IsNullOrWhiteSpace(expectedToken) &&
               string.Equals(CandidateToken, expectedToken, StringComparison.Ordinal) &&
               PostUpgradeManifest.IsSupersetOf(baseline);
    }
}

public sealed class UpgradeHealthReceiptStore
{
    private readonly string filePath;
    private readonly AtomicJsonFileStore atomicFileStore = new();

    public UpgradeHealthReceiptStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        this.filePath = filePath;
    }

    public async Task SaveAsync(UpgradeHealthReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        await atomicFileStore.WriteVerifiedAsync(
            filePath,
            receipt,
            value => JsonSerializer.Serialize(ToPersistence(value)),
            json => FromPersistence(JsonSerializer.Deserialize<PersistedReceipt>(json)
                ?? throw new InvalidDataException("升级健康回执为空。")),
            value =>
            {
                if (string.IsNullOrWhiteSpace(value.CandidateToken))
                    throw new InvalidDataException("升级健康回执缺少候选 token。");
                if (value.PostUpgradeManifest is null)
                    throw new InvalidDataException("升级健康回执缺少 manifest。");
            },
            cancellationToken);
    }

    public async Task<UpgradeHealthReceipt?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var persisted = JsonSerializer.Deserialize<PersistedReceipt>(json);
            return persisted is null ? null : FromPersistence(persisted);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static PersistedReceipt ToPersistence(UpgradeHealthReceipt receipt) =>
        new(
            receipt.CandidateToken,
            receipt.VerifiedAt,
            receipt.PostUpgradeManifest.HistoryIds.ToArray(),
            receipt.PostUpgradeManifest.TodoIds.ToArray(),
            receipt.PostUpgradeManifest.NoteIds.ToArray(),
            receipt.PostUpgradeManifest.AttachmentHashes.ToArray());

    private static UpgradeHealthReceipt FromPersistence(PersistedReceipt receipt) =>
        new(
            receipt.CandidateToken,
            new UpgradeDataManifest(receipt.HistoryIds, receipt.TodoIds, receipt.NoteIds, receipt.AttachmentHashes),
            receipt.VerifiedAt);

    private sealed record PersistedReceipt(
        string CandidateToken,
        DateTimeOffset VerifiedAt,
        string[] HistoryIds,
        string[] TodoIds,
        string[] NoteIds,
        string[] AttachmentHashes);
}
