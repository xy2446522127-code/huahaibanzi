using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpgradeHealthContractTests
{
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
