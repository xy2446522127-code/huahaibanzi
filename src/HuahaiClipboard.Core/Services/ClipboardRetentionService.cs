using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.Core.Services;

public sealed class ClipboardRetentionService(IClipboardHistorySource historySource)
{
    public async Task ApplyAsync(
        BehaviorSettings settings,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await historySource.PruneAsync(
            now.AddDays(-settings.AutoCleanupDays),
            preserveProtected: true,
            cancellationToken);
        if (settings.AutoCleanupCountEnabled)
        {
            await historySource.TrimOrdinaryAsync(settings.AutoCleanupCount, cancellationToken);
        }
    }
}
