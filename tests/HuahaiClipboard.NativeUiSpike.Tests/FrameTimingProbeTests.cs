using HuahaiClipboard.NativeUiSpike.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class FrameTimingProbeTests
{
    [TestMethod]
    public void Summarize_ReportsLiteralMeanP95AndDerivedFps()
    {
        var summary = FrameTimingProbe.Summarize([16, 17, 15, 20, 16]);

        Assert.AreEqual(5, summary.Count);
        Assert.AreEqual(16.8, summary.MeanIntervalMs, 0.001);
        Assert.AreEqual(20, summary.P95IntervalMs, 0.001);
        Assert.AreEqual(59.524, summary.DerivedFps, 0.001);
    }
}
