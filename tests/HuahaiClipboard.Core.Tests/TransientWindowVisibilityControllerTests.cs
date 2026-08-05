using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TransientWindowVisibilityControllerTests
{
    [TestMethod]
    public void Show_MakesWindowTopmostBeforeShowingIt()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        controller.Show();

        CollectionAssert.AreEqual(
            new[] { "content:active", "topmost:on", "show" },
            host.Actions);
    }

    [TestMethod]
    public void Hide_RemovesTopmostBeforeHidingWindow()
    {
        var host = new RecordingTransientWindowHost();
        var controller = new TransientWindowVisibilityController(host);

        controller.Hide();

        CollectionAssert.AreEqual(
            new[] { "topmost:off", "hide", "content:suspended" },
            host.Actions);
    }

    private sealed class RecordingTransientWindowHost : ITransientWindowHost
    {
        public string[] Actions => actions.ToArray();

        private readonly List<string> actions = [];

        public void SetTopmost(bool enabled) =>
            actions.Add(enabled ? "topmost:on" : "topmost:off");

        public void SetContentActive(bool active) =>
            actions.Add(active ? "content:active" : "content:suspended");

        public void Show() => actions.Add("show");

        public void Hide() => actions.Add("hide");
    }
}
