using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class PointerDoubleClickDetectorTests
{
    [TestMethod]
    public void SecondRightClickWithinTimeAndDistanceTriggersOnce()
    {
        var detector = new PointerDoubleClickDetector(500, 4, 4);

        Assert.IsFalse(detector.RegisterDown(1000, 200, 300));
        Assert.IsTrue(detector.RegisterDown(1350, 203, 297));
        Assert.IsFalse(detector.RegisterDown(1500, 203, 297));
    }

    [TestMethod]
    public void SlowOrDistantClicksStartANewSequence()
    {
        var detector = new PointerDoubleClickDetector(500, 4, 4);

        Assert.IsFalse(detector.RegisterDown(1000, 200, 300));
        Assert.IsFalse(detector.RegisterDown(1600, 200, 300));
        Assert.IsFalse(detector.RegisterDown(1800, 210, 300));
        Assert.IsTrue(detector.RegisterDown(2000, 211, 301));
    }
}
