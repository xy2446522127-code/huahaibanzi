using System.Reflection;
using HuahaiClipboard.App.Infrastructure.Tray;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.App.TrayTests;

[TestClass]
public sealed class TrayServiceTests
{
    [TestMethod]
    public void MenuItemsDispatchPanelSettingsAndExitCommands()
    {
        Exception? threadFailure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var panelCalls = 0;
                var settingsCalls = 0;
                var exitCalls = 0;
                using var service = new TrayService(
                    () => panelCalls++,
                    () => settingsCalls++,
                    () => exitCalls++);
                var field = typeof(TrayService).GetField(
                    "notifyIcon",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var icon = (System.Windows.Forms.NotifyIcon?)field?.GetValue(service);
                Assert.IsNotNull(icon);
                Assert.IsTrue(icon.Visible);
                Assert.AreEqual("花海剪贴板", icon.Text);

                var items = icon.ContextMenuStrip?.Items;
                Assert.IsNotNull(items);
                Assert.AreEqual(4, items.Count);
                Assert.AreEqual("显示面板", items[0].Text);
                Assert.AreEqual("设置", items[1].Text);
                Assert.AreEqual("退出", items[3].Text);

                items[0].PerformClick();
                items[1].PerformClick();
                items[3].PerformClick();

                Assert.AreEqual(1, panelCalls);
                Assert.AreEqual(1, settingsCalls);
                Assert.AreEqual(1, exitCalls);
            }
            catch (Exception error)
            {
                threadFailure = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(15)), "Tray test thread timed out.");
        if (threadFailure is not null)
        {
            Assert.Fail(threadFailure.ToString());
        }
    }
}
