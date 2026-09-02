namespace HuahaiClipboard.Core.Services;

public interface IDataLocationRegistry
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);

    Task WriteAsync(string dataRoot, CancellationToken cancellationToken);
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
