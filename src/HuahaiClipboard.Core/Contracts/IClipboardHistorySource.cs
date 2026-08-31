using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardHistorySource
{
    Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken);
    Task<ClipboardRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken);
    Task UpsertAsync(ClipboardRecord record, CancellationToken cancellationToken);
    Task<PreviewEditResult> ApplyPreviewEditAsync(
        Guid recordId,
        PreviewEdit edit,
        CancellationToken cancellationToken) => Task.FromResult(PreviewEditResult.RecordMissing());
    Task TouchAsync(Guid recordId, DateTimeOffset touchedAt, CancellationToken cancellationToken) =>
        Task.CompletedTask;
    Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task DeleteAsync(Guid recordId, CancellationToken cancellationToken);
    Task ClearUnprotectedAsync(CancellationToken cancellationToken);
    Task PruneAsync(DateTimeOffset cutoff, bool preserveProtected, CancellationToken cancellationToken);
    Task TrimOrdinaryAsync(int maximumCount, CancellationToken cancellationToken) => Task.CompletedTask;
    Task ClearAsync(CancellationToken cancellationToken);
}
