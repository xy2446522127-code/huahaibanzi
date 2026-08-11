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
    private readonly ClipboardImagePreviewSourceService imagePreviewSource;
    private readonly WindowsClipboardPlatform clipboardPlatform;
    private readonly WindowsClipboardWriteOriginGuard clipboardWriteOriginGuard;
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
        imageStore = new ProtectedClipboardImageStore(dataLayout.ImageDirectory, protector);
        historySource = new JsonClipboardHistorySource(
            dataLayout.HistoryFile,
            protector,
            imageStore);
        imagePreviewSource = new ClipboardImagePreviewSourceService(imageStore);
        clipboardWriteOriginGuard = new WindowsClipboardWriteOriginGuard();
        clipboardPlatform = new WindowsClipboardPlatform(imageStore, clipboardWriteOriginGuard);
        actionSink = new ClipboardPanelActionSink(historySource, clipboardPlatform);
        captureService = new ClipboardCaptureService(
            historySource,
            settingsStore,
            imageStore,
            clipboardWriteOriginGuard);
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

    public ClipboardImagePreviewSourceService ImagePreviewSource => imagePreviewSource;

    public LocalDataLayout DataLayout => dataLayout;
}
