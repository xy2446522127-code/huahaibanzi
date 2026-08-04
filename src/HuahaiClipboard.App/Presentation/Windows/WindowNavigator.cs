using HuahaiClipboard.Core.Contracts;

namespace HuahaiClipboard.App.Presentation.Windows;

public sealed class WindowNavigator : IWindowNavigator
{
    public Action? CursorPanelAction { get; set; }
    public Action? EdgePanelAction { get; set; }
    public Action? SettingsAction { get; set; }
    public Action? HideTransientPanelAction { get; set; }

    public void ShowCursorPanel() => CursorPanelAction?.Invoke();

    public void ShowEdgePanel() => EdgePanelAction?.Invoke();

    public void ShowSettings() => SettingsAction?.Invoke();

    public void HideTransientPanel() => HideTransientPanelAction?.Invoke();
}
