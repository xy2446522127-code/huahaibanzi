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

    [DataTestMethod]
    [DataRow(430, 680, 29, 96u, 0.8, 46)]
    [DataRow(688, 1088, 29, 96u, 1.6, 93)]
    public void RoundedRegion_ScalesWithTheWholePanel(
        int width,
        int height,
        int cornerRadius,
        uint dpi,
        double panelScale,
        int expectedDiameter)
    {
        var geometry = WindowChromeGeometry.Create(width, height, cornerRadius, dpi, panelScale);

        Assert.AreEqual(expectedDiameter, geometry.CornerDiameter);
    }

    [DataTestMethod]
    [DataRow(430, 680, 29, 144u, 0.8, 46)]
    [DataRow(820, 650, 29, 144u, 1.6, 93)]
    public void WebViewRegion_DoesNotDoubleScaleCssCornerRadiusByDpi(
        int width,
        int height,
        int cornerRadius,
        uint dpi,
        double panelScale,
        int expectedDiameter)
    {
        var geometry = WindowChromeGeometry.CreateForWebView(
            width,
            height,
            cornerRadius,
            dpi,
            panelScale);

        Assert.AreEqual(expectedDiameter, geometry.CornerDiameter);
    }

}
