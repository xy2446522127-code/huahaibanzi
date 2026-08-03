using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.Core.Contracts;

public interface ISettingsStore
{
    Task<ShellSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(ShellSettings settings, CancellationToken cancellationToken);
}
