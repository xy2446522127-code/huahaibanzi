using HuahaiClipboard.App.Infrastructure.Mocks;
using HuahaiClipboard.App.Presentation.Windows;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Presentation;

namespace HuahaiClipboard.App;

public sealed class CompositionRoot
{
    private readonly MockClipboardHistorySource historySource = new();
    private readonly MockPanelActionSink actionSink = new();
    private readonly MemorySettingsStore settingsStore = new();

    public PanelViewModel CreatePanel(WindowNavigator navigator) =>
        new(historySource, actionSink, navigator);

    public SettingsViewModel CreateSettings() => new(settingsStore);
}
