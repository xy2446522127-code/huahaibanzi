using System.Text.Json;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public sealed class JsonClipboardHistorySource : IClipboardHistorySource
{
    private readonly string filePath;
    private readonly ITextProtector protector;
    private readonly SemaphoreSlim gate = new(1, 1);
    private List<ClipboardRecord>? records;

    public JsonClipboardHistorySource(string filePath, ITextProtector protector)
    {
        this.filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            return records!.OrderByDescending(record => record.LastCopiedAt).ToArray();
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
            return records!.FirstOrDefault(record => record.Id == recordId);
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

            var cutoff = DateTimeOffset.Now.AddDays(-7);
            values.RemoveAll(value => !value.IsFavorite && !value.IsPinned && value.LastCopiedAt < cutoff);
            var overflow = values
                .Where(value => !value.IsFavorite && !value.IsPinned)
                .OrderByDescending(value => value.LastCopiedAt)
                .Skip(1000)
                .Select(value => value.Id)
                .ToHashSet();
            values.RemoveAll(value => overflow.Contains(value.Id));
        }, cancellationToken);
    }

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
            mutation(records!);
            await SaveAsync(cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (records is not null)
        {
            return;
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
            QuarantineCorruptHistory();
            records = [];
        }
    }

    private void QuarantineCorruptHistory()
    {
        var recoveryPath = filePath + ".corrupt";
        for (var suffix = 2; File.Exists(recoveryPath); suffix++)
        {
            recoveryPath = filePath + $".corrupt.{suffix}";
        }

        File.Move(filePath, recoveryPath);
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
