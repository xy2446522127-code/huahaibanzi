using System.Windows;

namespace HuahaiClipboard.NativeUiSpike.Presentation;

public interface IPanelWindowHost
{
    void CloseSettings();

    void FocusSearch();

    void HideWindow();

    void MoveNear(Point cursor);

    void OpenSettings();

    void RefreshContent();

    void SetTopmost(bool enabled);

    void ShowWindow();
}

public sealed class PanelWindowStateController(IPanelWindowHost host)
{
    public void ShowAt(Point cursor)
    {
        host.RefreshContent();
        host.MoveNear(cursor);
        host.SetTopmost(true);
        host.ShowWindow();
        host.FocusSearch();
    }

    public void Hide()
    {
        host.SetTopmost(false);
        host.HideWindow();
    }

    public void OpenSettings()
    {
        host.OpenSettings();
        host.SetTopmost(true);
    }

    public void CloseSettings()
    {
        host.CloseSettings();
        host.SetTopmost(true);
        host.FocusSearch();
    }
}
