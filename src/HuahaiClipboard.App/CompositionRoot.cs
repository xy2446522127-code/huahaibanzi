using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.App.Infrastructure.Storage;
using HuahaiClipboard.App.Presentation.Windows;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Services;

namespace HuahaiClipboard.App;

public sealed class CompositionRoot
{
    private readonly LocalDataLayout dataLayout;
    private readonly JsonClipboardHistorySource historySource;
    private readonly ProtectedClipboardImageStore imageStore;
    private readonly WindowsClipboardPlatform clipboardPlatform;
    private readonly ClipboardPanelActionSink actionSink;
    private readonly JsonSettingsStore settingsStore;
    private readonly ClipboardCaptureService captureService;

    public CompositionRoot()
    {
        dataLayout = new LocalDataLayout(
            LocalDataLayout.ResolveInstallRoot(),
            LocalDataLayout.ResolveUserKey());
        LocalDataMigrator.MigrateIfNeeded(
            dataLayout,
            LocalDataLayout.ResolveLegacyDataDirectory());
        var protector = new DpapiTextProtector();
        settingsStore = new JsonSettingsStore(dataLayout.SettingsFile);
        historySource = new JsonClipboardHistorySource(
            dataLayout.HistoryFile,
            protector);
        imageStore = new ProtectedClipboardImageStore(dataLayout.ImageDirectory, protector);
        clipboardPlatform = new WindowsClipboardPlatform(imageStore);
        actionSink = new ClipboardPanelActionSink(historySource, clipboardPlatform);
        captureService = new ClipboardCaptureService(
            historySource,
            settingsStore,
            imageStore);
    }

    public PanelViewModel CreatePanel(WindowNavigator navigator) =>
        new(historySource, actionSink, navigator);

    public SettingsViewModel CreateSettings() => new(settingsStore);

    public JsonSettingsStore SettingsStore => settingsStore;

    public ClipboardCaptureService CaptureService => captureService;

    public JsonClipboardHistorySource HistorySource => historySource;

    public ClipboardPanelActionSink ActionSink => actionSink;

    public WindowsClipboardPlatform ClipboardPlatform => clipboardPlatform;

    public ProtectedClipboardImageStore ImageStore => imageStore;

    public LocalDataLayout DataLayout => dataLayout;
}
