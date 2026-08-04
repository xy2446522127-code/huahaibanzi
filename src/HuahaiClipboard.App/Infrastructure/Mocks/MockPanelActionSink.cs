using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.App.Infrastructure.Mocks;

public sealed class MockPanelActionSink : IPanelActionSink
{
    private const string InvalidRecordMessage = "记录不存在或已删除";
    private const string ManualPasteMessage = "已复制，请按 Ctrl+V 手动粘贴";

    private static readonly Guid ManualPasteRecordId =
        Guid.Parse("00000000-0000-0000-0000-000000000012");

    private static readonly HashSet<Guid> ValidRecordIds = Enumerable
        .Range(1, 12)
        .Select(value => Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))
        .ToHashSet();

    public Task<PanelActionResult> CopyAsync(Guid recordId, CancellationToken cancellationToken) =>
        ResolveAsync(recordId, cancellationToken);

    public Task<PanelActionResult> PasteAsync(Guid recordId, CancellationToken cancellationToken) =>
        ResolveAsync(recordId, cancellationToken);

    private static Task<PanelActionResult> ResolveAsync(
        Guid recordId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!ValidRecordIds.Contains(recordId))
        {
            return Task.FromResult(PanelActionResult.Failure(InvalidRecordMessage));
        }

        return Task.FromResult(
            recordId == ManualPasteRecordId
                ? PanelActionResult.Failure(ManualPasteMessage)
                : PanelActionResult.Success());
    }
}
