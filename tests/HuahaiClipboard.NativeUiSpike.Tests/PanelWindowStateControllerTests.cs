using System.Windows;
using HuahaiClipboard.NativeUiSpike.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class PanelWindowStateControllerTests
{
    [TestMethod]
    public void ShowAt_RefreshesAndRaisesWindowBeforeFocusingSearch()
    {
        var host = new RecordingPanelWindowHost();
        var controller = new PanelWindowStateController(host);

        controller.ShowAt(new Point(1200, 500));

        CollectionAssert.AreEqual(
            new[] { "refresh", "move:1200,500", "topmost:on", "show", "focus" },
            host.Actions);
    }

    [TestMethod]
    public void Hide_RemovesTopmostBeforeHidingWindow()
    {
        var host = new RecordingPanelWindowHost();
        var controller = new PanelWindowStateController(host);

        controller.Hide();

        CollectionAssert.AreEqual(new[] { "topmost:off", "hide" }, host.Actions);
    }

    [TestMethod]
    public void SettingsNavigation_RemainsInsideVisibleTopmostWindow()
    {
        var host = new RecordingPanelWindowHost();
        var controller = new PanelWindowStateController(host);

        controller.OpenSettings();
        controller.CloseSettings();

        CollectionAssert.AreEqual(
            new[] { "settings:open", "topmost:on", "settings:close", "topmost:on", "focus" },
            host.Actions);
    }

    private sealed class RecordingPanelWindowHost : IPanelWindowHost
    {
        public List<string> Actions { get; } = [];

        public void CloseSettings() => Actions.Add("settings:close");

        public void FocusSearch() => Actions.Add("focus");

        public void HideWindow() => Actions.Add("hide");

        public void MoveNear(Point cursor) => Actions.Add($"move:{cursor.X:0},{cursor.Y:0}");

        public void OpenSettings() => Actions.Add("settings:open");

        public void RefreshContent() => Actions.Add("refresh");

        public void SetTopmost(bool enabled) => Actions.Add(enabled ? "topmost:on" : "topmost:off");

        public void ShowWindow() => Actions.Add("show");
    }
}
