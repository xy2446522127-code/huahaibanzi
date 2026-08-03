using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.App.Infrastructure.Mocks;

public sealed class MemorySettingsStore : ISettingsStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ShellSettings _settings = ShellSettings.Default;
    private long _nextSaveSequence;
    private long _latestAppliedSaveSequence;

    public async Task<ShellSettings> LoadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return Snapshot(_settings);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(
        ShellSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var saveSequence = Interlocked.Increment(ref _nextSaveSequence);
        var snapshot = Snapshot(settings);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (saveSequence > _latestAppliedSaveSequence)
            {
                _settings = snapshot;
                _latestAppliedSaveSequence = saveSequence;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static ShellSettings Snapshot(ShellSettings settings) =>
        settings with
        {
            Appearance = settings.Appearance with { },
            Motion = settings.Motion with { }
        };
}
