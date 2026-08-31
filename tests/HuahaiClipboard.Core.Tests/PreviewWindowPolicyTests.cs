using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class PreviewWindowPolicyTests
{
    [TestMethod]
    public void ShortcutLease_RequiresVisiblePanelHoveredRecordClosedSettingsAndDistinctKeyboardShortcut()
    {
        Assert.IsTrue(PreviewShortcutLeasePolicy.ShouldLease(
            mainPanelVisible: true,
            hasHoveredRecord: true,
            settingsOpen: false,
            previewShortcut: "Ctrl+Alt+P",
            summonShortcut: "Ctrl+Shift+V"));
        Assert.IsFalse(PreviewShortcutLeasePolicy.ShouldLease(true, false, false, "Ctrl+Alt+P", "Ctrl+Shift+V"));
        Assert.IsFalse(PreviewShortcutLeasePolicy.ShouldLease(true, true, true, "Ctrl+Alt+P", "Ctrl+Shift+V"));
        Assert.IsFalse(PreviewShortcutLeasePolicy.ShouldLease(true, true, false, "鼠标右键", "Ctrl+Shift+V"));
        Assert.IsFalse(PreviewShortcutLeasePolicy.ShouldLease(true, true, false, "CTRL + SHIFT + V", "Ctrl+Shift+V"));
    }

    [TestMethod]
    public void PlacementClamp_UsesDefaultSizeAndKeepsSavedPreviewInsideWorkArea()
    {
        var workArea = new PreviewWorkArea(100, 80, 1200, 800);

        var defaultPlacement = PreviewWindowPlacementStore.Clamp(null, "display-a", workArea);
        var clamped = PreviewWindowPlacementStore.Clamp(
            new PreviewWindowPlacement("removed-display", -1000, 2000, 50, 5000, Topmost: false),
            "display-a",
            workArea);

        Assert.AreEqual(new PreviewWindowPlacement("display-a", 375, 230, 650, 500, true), defaultPlacement);
        Assert.AreEqual("display-a", clamped.DisplayId);
        Assert.AreEqual(420, clamped.Width);
        Assert.AreEqual(768, clamped.Height);
        Assert.AreEqual(116, clamped.X);
        Assert.AreEqual(96, clamped.Y);
        Assert.IsFalse(clamped.Topmost);
    }

    [TestMethod]
    public async Task PlacementStore_PersistsLastPreviewGeometryIndependently()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-preview-placement-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "preview-window.json");
        try
        {
            var expected = new PreviewWindowPlacement("display-b", 444, 180, 720, 540, Topmost: false);
            var store = new PreviewWindowPlacementStore(path);

            await store.SaveAsync(expected, CancellationToken.None);

            Assert.AreEqual(expected, await new PreviewWindowPlacementStore(path).LoadLastAsync(CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
