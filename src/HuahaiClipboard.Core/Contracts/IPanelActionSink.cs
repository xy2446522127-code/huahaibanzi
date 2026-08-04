using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Contracts;

public interface IPanelActionSink
{
    Task<PanelActionResult> CopyAsync(Guid recordId, CancellationToken cancellationToken);
    Task<PanelActionResult> PasteAsync(Guid recordId, CancellationToken cancellationToken);
}
