using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardPlatform
{
    Task WriteAsync(ClipboardRecord record, CancellationToken cancellationToken);
    Task<bool> PasteAsync(CancellationToken cancellationToken);
}
