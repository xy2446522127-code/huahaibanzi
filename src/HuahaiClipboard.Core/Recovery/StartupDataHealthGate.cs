namespace HuahaiClipboard.Core.Recovery;

public enum StartupDataState
{
    Healthy,
    RecoveryRequired
}

public sealed record StartupDataHealthResult(StartupDataState State, bool CaptureAllowed);

public sealed class StartupDataHealthGate
{
    public StartupDataHealthResult Evaluate(
        string expectedToken,
        UpgradeDataManifest baseline,
        UpgradeHealthReceipt? receipt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedToken);
        ArgumentNullException.ThrowIfNull(baseline);

        var healthy = receipt is not null && receipt.Verifies(expectedToken, baseline);
        return healthy
            ? new StartupDataHealthResult(StartupDataState.Healthy, true)
            : new StartupDataHealthResult(StartupDataState.RecoveryRequired, false);
    }
}
