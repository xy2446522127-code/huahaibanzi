using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardHistorySource
{
    Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken);
    Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken);
    Task DeleteAsync(Guid recordId, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
