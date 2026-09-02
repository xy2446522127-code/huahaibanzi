namespace HuahaiClipboard.Core.Recovery;

public enum RecoverySourceKind
{
    RegisteredDataRoot,
    InstallDataRoot,
    LegacyLocalAppData,
    Backup,
    CorruptHistory
}

public sealed record RecoverySource(string Root, RecoverySourceKind Kind, string Provenance);

public sealed record RecoveryDiscoveryHint(string Root, RecoverySourceKind Kind, string Provenance);

public enum RecoveryInspectionState
{
    Readable,
    EncryptedForAnotherUser,
    Malformed,
    Incomplete,
    Duplicate,
    Unavailable
}

public sealed record RecoveryDataSummary(int HistoryCount, int TodoCount, int NoteCount, int ImageCount);

public interface IRecoveryDataReader
{
    Task<RecoveryDataSummary> ReadAsync(string dataDirectory, CancellationToken cancellationToken);
}

public sealed record RecoveryInspection(
    RecoverySource Source,
    RecoveryInspectionState State,
    int HistoryCount,
    int TodoCount,
    int NoteCount,
    int ImageCount,
    IReadOnlyDictionary<string, string> FileManifest,
    string? ErrorCode);
