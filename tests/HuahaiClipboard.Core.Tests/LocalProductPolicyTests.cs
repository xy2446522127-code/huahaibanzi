using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class LocalProductPolicyTests
{
    [TestMethod]
    public void LocalDataLayout_UsesOneInspectablePerUserDirectory()
    {
        var layout = new LocalDataLayout(@"C:\Users\TestUser\AppData\Local");

        Assert.AreEqual(
            @"C:\Users\TestUser\AppData\Local\HuahaiClipboard",
            layout.DataDirectory);
        Assert.AreEqual(
            @"C:\Users\TestUser\AppData\Local\HuahaiClipboard\history.dat",
            layout.HistoryFile);
        Assert.AreEqual(
            @"C:\Users\TestUser\AppData\Local\HuahaiClipboard\settings.json",
            layout.SettingsFile);
        Assert.AreEqual(
            @"C:\Users\TestUser\AppData\Local\HuahaiClipboard\images",
            layout.ImageDirectory);
    }

    [TestMethod]
    public void ImageDisplayName_UsesAReadableTimestampInsteadOfDimensionsOrAnId()
    {
        var actual = ClipboardDisplayName.CreateImageFileName(
            DateTimeOffset.Parse("2026-08-04T16:42:05+08:00"));

        Assert.AreEqual("花海截图-20260804-164205.png", actual);
    }
}
