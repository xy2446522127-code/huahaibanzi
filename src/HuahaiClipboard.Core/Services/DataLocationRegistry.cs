namespace HuahaiClipboard.Core.Services;

using Microsoft.Win32;
using System.Runtime.Versioning;

public interface IDataLocationRegistry
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(string dataRoot, CancellationToken cancellationToken);
}

/// <summary>
/// Stores the stable data root in the current user's profile, independently
/// of the executable or uninstall registration path.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryDataLocationRegistry : IDataLocationRegistry
{
    public const string DefaultSubKey = "Software\\HuahaiClipboard";
    public const string ValueName = "DataLocation";

    private readonly string subKey;

    public WindowsRegistryDataLocationRegistry(string subKey = DefaultSubKey)
    {
        if (string.IsNullOrWhiteSpace(subKey) || subKey.StartsWith('\\') || subKey.EndsWith('\\'))
        {
            throw new ArgumentException("Registry subkey is invalid.", nameof(subKey));
        }

        this.subKey = subKey;
    }

    public Task<string?> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var key = Registry.CurrentUser.OpenSubKey(subKey, writable: false);
        var value = key?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if (string.IsNullOrWhiteSpace(value)) return Task.FromResult<string?>(null);

        try
        {
            return Task.FromResult<string?>(Path.GetFullPath(value.Trim()));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Task.FromResult<string?>(null);
        }
    }

    public Task WriteAsync(string dataRoot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        var normalized = Path.GetFullPath(dataRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        using var key = Registry.CurrentUser.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException("无法写入当前用户的数据位置注册表。");
        key.SetValue(ValueName, normalized, RegistryValueKind.String);
        return Task.CompletedTask;
    }
}

public enum DataRootResolutionKind
{
    Registered,
    InstallRoot,
    NewInstall,
    RecoveryRequired
}

public sealed record DataRootResolution(
    DataRootResolutionKind Kind,
    string? DataRoot,
    string? LegacyMigrationSource,
    IReadOnlyList<string> ConflictingDataRoots);
