using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpgradeHealthContractTests
{
    [TestMethod]
    public void Receipt_IsValidOnlyForMatchingTokenAndSupersetManifest()
    {
        var baseline = new UpgradeDataManifest(["h1"], ["t1"], ["n1"], ["a1"]);
        var receipt = new UpgradeHealthReceipt(
            "token-1",
            new UpgradeDataManifest(["h1", "h2"], ["t1"], ["n1"], ["a1", "a2"]),
            DateTimeOffset.UtcNow);

        Assert.IsTrue(receipt.Verifies("token-1", baseline));
        Assert.IsFalse(receipt.Verifies("token-2", baseline));
        Assert.IsFalse(receipt.Verifies("token-1", new UpgradeDataManifest(["h1", "h3"], ["t1"], ["n1"], ["a1"])));
    }

    [TestMethod]
    public async Task ReceiptStore_WritesAndReadsTokenBoundReceiptAtomically()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-receipt-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(root, "upgrade-health.json");
            var receipt = new UpgradeHealthReceipt(
                "token-1",
                new UpgradeDataManifest(["h1"], [], [], []),
                DateTimeOffset.UtcNow);

            var store = new UpgradeHealthReceiptStore(path);
            await store.SaveAsync(receipt, CancellationToken.None);
            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.AreEqual(receipt.CandidateToken, loaded!.CandidateToken);
            Assert.IsTrue(loaded.Verifies("token-1", new UpgradeDataManifest(["h1"], [], [], [])));
            Assert.IsTrue(File.Exists(path + ".bak1") == false);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IsSupersetOf_ReturnsTrueWhenEveryBaselineIdentityAndAttachmentSurvives()
    {
        var before = new UpgradeDataManifest(
            ["history-a"],
            ["todo-a"],
            ["note-a"],
            ["ABC123"]);
        var after = new UpgradeDataManifest(
            ["history-a", "history-b"],
            ["todo-a", "todo-b"],
            ["note-a", "note-b"],
            ["ABC123", "DEF456"]);

        Assert.IsTrue(after.IsSupersetOf(before));
    }

    [TestMethod]
    public void IsSupersetOf_ReturnsFalseWhenOneBaselineAttachmentHashIsMissing()
    {
        var before = new UpgradeDataManifest(
            ["history-a"],
            ["todo-a"],
            ["note-a"],
            ["ABC123"]);
        var after = new UpgradeDataManifest(
            ["history-a", "history-b"],
            ["todo-a", "todo-b"],
            ["note-a", "note-b"],
            ["DEF456"]);

        Assert.IsFalse(after.IsSupersetOf(before));
    }

    [TestMethod]
    public void IsSupersetOf_ReturnsFalseWhenCountsIncreaseButAnExistingRecordIdDisappears()
    {
        var before = new UpgradeDataManifest(
            ["history-a", "history-b"],
            [],
            [],
            []);
        var after = new UpgradeDataManifest(
            ["history-c", "history-d", "history-e"],
            [],
            [],
            []);

        Assert.IsFalse(after.IsSupersetOf(before));
    }
}
