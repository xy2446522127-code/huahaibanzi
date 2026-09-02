namespace HuahaiClipboard.Core.Recovery;

/// <summary>
/// UI-independent gate for recovery actions. A host can show discovery and
/// inspection results freely, but it cannot enable a data-changing action
/// until it has a readable source, two verified snapshots, and a plan.
/// </summary>
public sealed class RecoveryWorkflowState
{
    private RecoveryInspection? inspection;
    private RecoveryPlan? plan;
    private bool sourceSnapshotVerified;
    private bool destinationSnapshotVerified;

    public RecoveryInspection? Inspection => inspection;

    public RecoveryPlan? Plan => plan;

    public bool CanApply => inspection?.State == RecoveryInspectionState.Readable &&
                            sourceSnapshotVerified &&
                            destinationSnapshotVerified &&
                            plan is not null;

    public void SetInspection(RecoveryInspection value)
    {
        inspection = value ?? throw new ArgumentNullException(nameof(value));
        plan = null;
        sourceSnapshotVerified = false;
        destinationSnapshotVerified = false;
    }

    public void SetVerifiedSnapshots(bool sourceSnapshot, bool destinationSnapshot)
    {
        sourceSnapshotVerified = sourceSnapshot;
        destinationSnapshotVerified = destinationSnapshot;
        plan = null;
    }

    public void SetPlan(RecoveryPlan value)
    {
        plan = value ?? throw new ArgumentNullException(nameof(value));
    }
}
