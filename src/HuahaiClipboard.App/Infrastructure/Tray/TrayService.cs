using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace HuahaiClipboard.App.Infrastructure.Tray;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon notifyIcon;
    private readonly Forms.ToolStripMenuItem updateItem;

    public TrayService(Action showPanel, Action showSettings, Action showUpdate, Action exit)
    {
        ArgumentNullException.ThrowIfNull(showPanel);
        ArgumentNullException.ThrowIfNull(showSettings);
        ArgumentNullException.ThrowIfNull(showUpdate);
        ArgumentNullException.ThrowIfNull(exit);

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示面板", null, (_, _) => showPanel());
        menu.Items.Add("设置", null, (_, _) => showSettings());
        updateItem = new Forms.ToolStripMenuItem("发现新版本", null, (_, _) => showUpdate())
        {
            Available = false,
        };
        menu.Items.Add(updateItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        notifyIcon = new Forms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "花海剪贴板",
            ContextMenuStrip = menu,
            Visible = true,
        };
        notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                showPanel();
            }
        };
        notifyIcon.BalloonTipClicked += (_, _) => showUpdate();
    }

    public void SetUpdateAvailable(Version? latestVersion)
    {
        updateItem.Available = latestVersion is not null;
        updateItem.Text = latestVersion is null
            ? "发现新版本"
            : $"发现新版本 v{latestVersion.ToString(3)}";
    }

    public void NotifyUpdateAvailable(Version latestVersion)
    {
        ArgumentNullException.ThrowIfNull(latestVersion);
        SetUpdateAvailable(latestVersion);
        notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        notifyIcon.BalloonTipTitle = "花海剪贴板有新版本";
        notifyIcon.BalloonTipText = $"发现 v{latestVersion.ToString(3)}，点击查看并更新。";
        notifyIcon.ShowBalloonTip(8000);
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }
}
