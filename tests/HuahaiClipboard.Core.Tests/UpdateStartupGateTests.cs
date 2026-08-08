using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpdateStartupGateTests
{
    [TestMethod]
    public void OpensOnceWhenShellBecomesReadyBeforeTray()
    {
        var gate = new UpdateStartupGate();

        Assert.IsFalse(gate.TryBegin(shellReady: true, trayReady: false));
        Assert.IsTrue(gate.TryBegin(shellReady: true, trayReady: true));
        Assert.IsFalse(gate.TryBegin(shellReady: true, trayReady: true));
    }

    [TestMethod]
    public void OpensOnceWhenTrayBecomesReadyBeforeShell()
    {
        var gate = new UpdateStartupGate();

        Assert.IsFalse(gate.TryBegin(shellReady: false, trayReady: true));
        Assert.IsTrue(gate.TryBegin(shellReady: true, trayReady: true));
        Assert.IsFalse(gate.TryBegin(shellReady: true, trayReady: true));
    }
}
