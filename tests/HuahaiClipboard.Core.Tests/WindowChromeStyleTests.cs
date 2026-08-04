using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class WindowChromeStyleTests
{
    [TestMethod]
    public void WithoutNativeFrame_RemovesOnlyFrameFlags()
    {
        const long visible = 0x10000000;
        const long minimizeBox = 0x00020000;
        const long caption = 0x00C00000;
        const long thickFrame = 0x00040000;
        const long extendedWindowEdge = 0x00000100;
        const long extendedClientEdge = 0x00000200;

        var result = WindowChromeStyle.WithoutNativeFrame(
            visible | minimizeBox | caption | thickFrame,
            extendedWindowEdge | extendedClientEdge);

        Assert.AreEqual(visible | minimizeBox, result.Style);
        Assert.AreEqual(0, result.ExtendedStyle);
    }
}
