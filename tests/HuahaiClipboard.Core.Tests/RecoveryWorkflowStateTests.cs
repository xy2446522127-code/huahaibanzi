using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoveryWorkflowStateTests
{
    [TestMethod]
    public void CanApply_IsFalseUntilReadableSourceAndVerifiedSnapshotsAndPlanExist()
    {
        var state = new RecoveryWorkflowState();

        Assert.IsFalse(state.CanApply);

        state.SetInspection(new RecoveryInspection(
            new RecoverySource(@"F:\\old-data", RecoverySourceKind.LegacyLocalAppData, "legacy"),
            RecoveryInspectionState.Readable,
            1,
            0,
            0,
            0,
            new Dictionary<string, string>(),
            null));
        Assert.IsFalse(state.CanApply);

        state.SetVerifiedSnapshots(sourceSnapshot: true, destinationSnapshot: true);
        Assert.IsFalse(state.CanApply);

        state.SetPlan(new RecoveryPlan([], HuahaiClipboard.Core.Todo.TodoWorkspace.Empty, []));
        Assert.IsTrue(state.CanApply);
    }

    [TestMethod]
    public void CanApply_IsFalseForUnreadableSourceEvenWhenPlanAndSnapshotsExist()
    {
        var state = new RecoveryWorkflowState();
        state.SetInspection(new RecoveryInspection(
            new RecoverySource(@"F:\\old-data", RecoverySourceKind.LegacyLocalAppData, "legacy"),
            RecoveryInspectionState.EncryptedForAnotherUser,
            0,
            0,
            0,
            0,
            new Dictionary<string, string>(),
            "dpapi-user-mismatch"));
        state.SetVerifiedSnapshots(sourceSnapshot: true, destinationSnapshot: true);
        state.SetPlan(new RecoveryPlan([], HuahaiClipboard.Core.Todo.TodoWorkspace.Empty, []));

        Assert.IsFalse(state.CanApply);
    }
}
