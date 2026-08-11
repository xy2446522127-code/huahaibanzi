using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ClipboardWriteOriginStateTests
{
    [TestMethod]
    public void Matches_RequiresBothTheProcessTokenAndRecordedSequence()
    {
        var state = new ClipboardWriteOriginState("process-token");

        state.Record(42);

        Assert.IsTrue(state.Matches("process-token", 42));
        Assert.IsFalse(state.Matches("different-token", 42));
        Assert.IsFalse(state.Matches("process-token", 43));
        Assert.IsFalse(state.Matches(null, 42));
    }

    [TestMethod]
    public void Matches_NeverSuppressesAnUnrecordedClipboardSequence()
    {
        var state = new ClipboardWriteOriginState("process-token");

        Assert.IsFalse(state.Matches("process-token", 0));

        state.Record(0);

        Assert.IsFalse(state.Matches("process-token", 0));
    }
}
