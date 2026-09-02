using System.Text.Json;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public sealed class HistoryRecoveryRequiredException(string message, Exception innerException) : IOException(message, innerException);

public sealed class JsonClipboardHistorySource : IClipboardHistorySource
{
    private readonly string filePath;
    private readonly ITextProtector protector;
    private readonly IClipboardImageStore? imageStore;
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<ClipboardRecord>? records;
    private Exception? loadFailure;

    public JsonClipboardHistorySource(
        string filePath,
        ITextProtector protector,
        IClipboardImageStore? imageStore = null)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
        this.imageStore = imageStore;
    }

    public async Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            return records!
                .OrderByDescending(record => record.IsPinned)
                .ThenByDescending(record => record.LastCopiedAt)
                .ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ClipboardRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            return records!.FirstOrDefault(record => record.Id == recordId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<PreviewEditResult> ApplyPreviewEditAsync(
        Guid recordId,
        PreviewEdit edit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            var index = records!.FindIndex(record => record.Id == recordId);
            if (index < 0)
            {
                return PreviewEditResult.RecordMissing();
            }

            var result = ClipboardRecordEditor.Apply(records[index], edit);
            if (!result.Succeeded)
            {
                return result;
            }

            records[index] = result.Record!;
            await SaveAsync(cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task UpsertAsync(ClipboardRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await MutateAsync(values =>
        {
            var index = values.FindIndex(existing =>
                existing.Kind == record.Kind &&
                string.Equals(existing.PrimaryText, record.PrimaryText, StringComparison.Ordinal));
            if (index >= 0)
            {
                var existing = values[index];
                values[index] = record with
                {
                    Id = existing.Id,
                    IsFavorite = existing.IsFavorite,
                    IsPinned = existing.IsPinned
                };
            }
            else
            {
                values.Add(record);
            }

            var overflow = values
                .Where(value => !value.IsFavorite && !value.IsPinned)
                .OrderByDescending(value => value.LastCopiedAt)
                .Skip(10000)
                .Select(value => value.Id)
                .ToHashSet();
            values.RemoveAll(value => overflow.Contains(value.Id));
        }, cancellationToken);
    }

    public Task TouchAsync(Guid recordId, DateTimeOffset touchedAt, CancellationToken cancellationToken) =>
        UpdateAsync(recordId, record => record with { LastCopiedAt = touchedAt }, cancellationToken);

    public Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken) =>
        UpdateAsync(recordId, record => record with { IsFavorite = value }, cancellationToken);

    public Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken) =>
        UpdateAsync(recordId, record => record with { IsPinned = value }, cancellationToken);

    public Task DeleteAsync(Guid recordId, CancellationToken cancellationToken) =>
        MutateAsync(values =>
        {
            var removed = values.RemoveAll(record => record.Id == recordId);
            if (removed == 0)
            {
                throw new KeyNotFoundException($"Clipboard record '{recordId}' does not exist.");
            }
        }, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken) =>
        MutateAsync(values => values.Clear(), cancellationToken);

    public Task ClearUnprotectedAsync(CancellationToken cancellationToken) =>
        MutateAsync(
            values => values.RemoveAll(value => !value.IsFavorite && !value.IsPinned),
            cancellationToken);

    public async Task PruneAsync(
        DateTimeOffset cutoff,
        bool preserveProtected,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            var beforeAssets = ReferencedAssets(records!);
            var removed = records!.RemoveAll(value =>
                value.LastCopiedAt < cutoff &&
                (!preserveProtected || !value.IsFavorite && !value.IsPinned));
            if (removed > 0)
            {
                await SaveAsync(cancellationToken);
                await DeleteRemovedAssetsAsync(beforeAssets, records!, cancellationToken);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task TrimOrdinaryAsync(int maximumCount, CancellationToken cancellationToken)
    {
        if (maximumCount is < 1 or > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            var overflow = records!
                .Where(value => !value.IsFavorite && !value.IsPinned)
                .OrderByDescending(value => value.LastCopiedAt)
                .Skip(maximumCount)
                .Select(value => value.Id)
                .ToHashSet();
            if (overflow.Count == 0) return;
            var beforeAssets = ReferencedAssets(records!);
            records!.RemoveAll(value => overflow.Contains(value.Id));
            await SaveAsync(cancellationToken);
            await DeleteRemovedAssetsAsync(beforeAssets, records!, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task UpdateAsync(
        Guid recordId,
        Func<ClipboardRecord, ClipboardRecord> update,
        CancellationToken cancellationToken) =>
        MutateAsync(values =>
        {
            var index = values.FindIndex(record => record.Id == recordId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"Clipboard record '{recordId}' does not exist.");
            }

            values[index] = update(values[index]);
        }, cancellationToken);

    private async Task MutateAsync(Action<List<ClipboardRecord>> mutation, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            ThrowIfRecoveryRequired();
            var beforeAssets = ReferencedAssets(records!);
            mutation(records!);
            await SaveAsync(cancellationToken);
            await DeleteRemovedAssetsAsync(beforeAssets, records!, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task DeleteRemovedAssetsAsync(
        IReadOnlySet<string> beforeAssets,
        IReadOnlyCollection<ClipboardRecord> remaining,
        CancellationToken cancellationToken)
    {
        if (imageStore is null || beforeAssets.Count == 0) return;
        var remainingAssets = ReferencedAssets(remaining);
        foreach (var path in beforeAssets.Where(path => !remainingAssets.Contains(path)))
        {
            try
            {
                await imageStore.DeleteAsync(path, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Startup orphan collection retries files temporarily held by another process.
            }
        }
    }

    private static HashSet<string> ReferencedAssets(IEnumerable<ClipboardRecord> values) =>
        values
            .Select(value => value.PreviewAssetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (records is not null)
        {
            return;
        }

        if (loadFailure is not null)
        {
            ThrowIfRecoveryRequired();
        }

        if (!File.Exists(filePath))
        {
            records = [];
            return;
        }

        try
        {
            var protectedJson = await File.ReadAllTextAsync(filePath, cancellationToken);
            var json = protector.Unprotect(protectedJson);
            records = (JsonSerializer.Deserialize<List<ClipboardRecord>>(json) ?? [])
                .Select(NormalizeLegacyRecord)
                .ToList();
        }
        catch (Exception exception) when (exception is JsonException or FormatException or System.Security.Cryptography.CryptographicException)
        {
            loadFailure = exception;
        }
    }

    private void ThrowIfRecoveryRequired()
    {
        if (loadFailure is not null)
        {
            throw new HistoryRecoveryRequiredException(
                "剪贴板历史数据无法读取，需要进入恢复流程。",
                loadFailure);
        }
    }

    private static ClipboardRecord NormalizeLegacyRecord(ClipboardRecord record)
    {
        var isLegacyImageTitle = record.Kind == ClipboardItemKind.Image &&
                                 record.PrimaryText.StartsWith("图片 ", StringComparison.Ordinal) &&
                                 record.PrimaryText.Contains(" x ", StringComparison.Ordinal);
        return isLegacyImageTitle
            ? record with { PrimaryText = ClipboardDisplayName.CreateImageFileName(record.LastCopiedAt) }
            : record;
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(records);
        var protectedJson = protector.Protect(json);
        var temporaryPath = filePath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, protectedJson, cancellationToken);
        File.Move(temporaryPath, filePath, overwrite: true);
    }
}
