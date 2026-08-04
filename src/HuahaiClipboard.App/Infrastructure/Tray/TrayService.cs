using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace HuahaiClipboard.App.Infrastructure.Tray;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon notifyIcon;

    public TrayService(Action showPanel, Action showSettings, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示面板", null, (_, _) => showPanel());
        menu.Items.Add("设置", null, (_, _) => showSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exit());

        notifyIcon = new Forms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Text = "花海剪贴板",
            ContextMenuStrip = menu,
            Visible = true
        };
        notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == Forms.MouseButtons.Left)
            {
                showPanel();
            }
        };
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
    }
}
