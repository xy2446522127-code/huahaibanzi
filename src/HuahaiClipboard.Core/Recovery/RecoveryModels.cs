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
