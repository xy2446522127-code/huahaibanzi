using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class StartupDataHealthGateTests
{
    [TestMethod]
    public void Evaluate_BlocksCaptureWhenReceiptIsMissingOrInvalid()
    {
        var baseline = new UpgradeDataManifest(["h1"], [], [], []);
        var gate = new StartupDataHealthGate();

        Assert.AreEqual(StartupDataState.RecoveryRequired, gate.Evaluate("token-1", baseline, null).State);
        Assert.IsFalse(gate.Evaluate("token-1", baseline, null).CaptureAllowed);

        var invalid = new UpgradeHealthReceipt("other-token", baseline, DateTimeOffset.UtcNow);
        var result = gate.Evaluate("token-1", baseline, invalid);
        Assert.AreEqual(StartupDataState.RecoveryRequired, result.State);
        Assert.IsFalse(result.CaptureAllowed);
    }

    [TestMethod]
    public void Evaluate_AllowsCaptureOnlyForVerifiedSupersetReceipt()
    {
        var baseline = new UpgradeDataManifest(["h1"], [], [], []);
        var receipt = new UpgradeHealthReceipt(
            "token-1",
            new UpgradeDataManifest(["h1", "h2"], [], [], []),
            DateTimeOffset.UtcNow);

        var result = new StartupDataHealthGate().Evaluate("token-1", baseline, receipt);

        Assert.AreEqual(StartupDataState.Healthy, result.State);
        Assert.IsTrue(result.CaptureAllowed);
    }
}
