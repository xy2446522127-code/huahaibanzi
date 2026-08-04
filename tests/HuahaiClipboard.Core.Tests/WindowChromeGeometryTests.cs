using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class WindowChromeGeometryTests
{
    [DataTestMethod]
    [DataRow(430, 680, 29, 96u, 58)]
    [DataRow(430, 680, 29, 144u, 87)]
    [DataRow(40, 30, 29, 192u, 30)]
    public void RoundedRegion_ScalesWithDpiAndNeverExceedsTheWindow(
        int width,
        int height,
        int cornerRadius,
        uint dpi,
        int expectedDiameter)
    {
        var geometry = WindowChromeGeometry.Create(width, height, cornerRadius, dpi);

        Assert.AreEqual(width, geometry.Width);
        Assert.AreEqual(height, geometry.Height);
        Assert.AreEqual(expectedDiameter, geometry.CornerDiameter);
    }
}
