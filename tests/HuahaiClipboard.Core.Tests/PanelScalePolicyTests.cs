using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class PanelScalePolicyTests
{
    [DataTestMethod]
    [DataRow(0.79, 80)]
    [DataRow(0.805, 81)]
    [DataRow(0.834, 83)]
    [DataRow(1.174, 117)]
    [DataRow(1.494, 149)]
    [DataRow(1.594, 159)]
    [DataRow(1.61, 160)]
    public void Ratio_NormalizesToOnePercentPrecisionAcrossTheWholeRange(double ratio, int expectedPercent)
    {
        Assert.AreEqual(expectedPercent, PanelScalePolicy.NormalizePercent(ratio));
        Assert.AreEqual(expectedPercent / 100d, PanelScalePolicy.ToRatio(expectedPercent), 0.000001);
    }

    [TestMethod]
    public void InvalidRatio_RestoresTheOneHundredPercentDefault()
    {
        Assert.AreEqual(100, PanelScalePolicy.NormalizePercent(double.NaN));
        Assert.AreEqual(100, PanelScalePolicy.NormalizePercent(double.PositiveInfinity));
    }

    [TestMethod]
    public void PreviewSession_CancelRestoresTheLastCommittedRatio()
    {
        var session = new PanelScalePreviewSession(0.83);

        Assert.AreEqual(1.59, session.Preview(1.59), 0.000001);
        Assert.AreEqual(0.83, session.Cancel(), 0.000001);
        Assert.AreEqual(0.83, session.CurrentRatio, 0.000001);
        Assert.AreEqual(0.83, session.CommittedRatio, 0.000001);
    }

    [TestMethod]
    public void PreviewSession_CommitBecomesTheNextCancelTarget()
    {
        var session = new PanelScalePreviewSession(1.0);

        Assert.AreEqual(1.17, session.Commit(1.174), 0.000001);
        Assert.AreEqual(1.49, session.Preview(1.494), 0.000001);
        Assert.AreEqual(1.17, session.Cancel(), 0.000001);
    }
}
