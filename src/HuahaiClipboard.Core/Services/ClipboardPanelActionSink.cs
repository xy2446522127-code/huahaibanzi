using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public sealed class ClipboardPanelActionSink(
    IClipboardHistorySource historySource,
    IClipboardPlatform clipboardPlatform) : IPanelActionSink
{
    public Task<PanelActionResult> CopyAsync(Guid recordId, CancellationToken cancellationToken) =>
        RunAsync(recordId, paste: false, cancellationToken);

    public Task<PanelActionResult> PasteAsync(Guid recordId, CancellationToken cancellationToken) =>
        RunAsync(recordId, paste: true, cancellationToken);

    private async Task<PanelActionResult> RunAsync(
        Guid recordId,
        bool paste,
        CancellationToken cancellationToken)
    {
        var record = await historySource.FindAsync(recordId, cancellationToken);
        if (record is null)
        {
            return PanelActionResult.Failure("记录不存在或已删除");
        }

        await clipboardPlatform.WriteAsync(record, cancellationToken);
        if (!paste)
        {
            return PanelActionResult.Success();
        }

        return await clipboardPlatform.PasteAsync(cancellationToken)
            ? PanelActionResult.Success()
            : PanelActionResult.Failure("已复制，请按 Ctrl+V 手动粘贴");
    }
}
