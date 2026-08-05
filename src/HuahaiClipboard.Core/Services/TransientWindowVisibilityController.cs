namespace HuahaiClipboard.Core.Services;

public interface ITransientWindowHost
{
    void SetContentActive(bool active);

    void SetTopmost(bool enabled);

    void Show();

    void Hide();
}

public sealed class TransientWindowVisibilityController(ITransientWindowHost host)
{
    public void Show()
    {
        host.SetContentActive(true);
        host.SetTopmost(true);
        host.Show();
    }

    public void Hide()
    {
        host.SetTopmost(false);
        host.Hide();
        host.SetContentActive(false);
    }
}
