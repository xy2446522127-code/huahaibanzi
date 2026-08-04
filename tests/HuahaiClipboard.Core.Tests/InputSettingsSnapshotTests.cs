using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class InputSettingsSnapshotTests
{
    [TestMethod]
    public void Update_ReplacesTheSettingsReadByGlobalInputCallbacks()
    {
        var initial = new InputSettings(true, true, ["secret.exe"]);
        var updated = new InputSettings(false, false, ["game.exe"]);
        var snapshot = new InputSettingsSnapshot(initial);

        snapshot.Update(updated);

        Assert.AreSame(updated, snapshot.Current);
    }
}
