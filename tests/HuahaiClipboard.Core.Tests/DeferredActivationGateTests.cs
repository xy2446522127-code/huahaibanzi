using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class DeferredActivationGateTests
{
    [TestMethod]
    public void ReplaysOneActivationThatArrivedBeforeTheWindowWasReady()
    {
        var gate = new DeferredActivationGate();

        Assert.IsFalse(gate.RequestActivation());
        Assert.IsTrue(gate.MarkReady());
        Assert.IsFalse(gate.MarkReady());
    }

    [TestMethod]
    public void DispatchesImmediatelyAfterTheWindowIsReady()
    {
        var gate = new DeferredActivationGate();

        Assert.IsFalse(gate.MarkReady());
        Assert.IsTrue(gate.RequestActivation());
    }
}
