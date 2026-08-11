using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.App.Infrastructure.Startup;
using HuahaiClipboard.App.Infrastructure.Storage;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.NativeUiSpike.Presentation;

namespace HuahaiClipboard.NativeUiSpike.Production;

public sealed class ProductionCompositionRoot
{
    public ProductionCompositionRoot(string? localApplicationData = null)
    {
        DataLayout = new LocalDataLayout(
            localApplicationData ?? LocalDataLayout.ResolveInstallRoot(),
            LocalDataLayout.ResolveUserKey());
        var protector = new DpapiTextProtector();
        SettingsStore = new JsonSettingsStore(DataLayout.SettingsFile);
        HistorySource = new JsonClipboardHistorySource(DataLayout.HistoryFile, protector);
        ImageStore = new ProtectedClipboardImageStore(DataLayout.ImageDirectory, protector);
        var clipboardWriteOriginGuard = new WindowsClipboardWriteOriginGuard();
        ClipboardPlatform = new WindowsClipboardPlatform(ImageStore, clipboardWriteOriginGuard);
        ActionSink = new ClipboardPanelActionSink(HistorySource, ClipboardPlatform);
        CaptureService = new ClipboardCaptureService(
            HistorySource,
            SettingsStore,
            ImageStore,
            clipboardWriteOriginGuard);
        StartupService = new StartupRegistrationService();
    }

    public LocalDataLayout DataLayout { get; }

    public JsonSettingsStore SettingsStore { get; }

    public JsonClipboardHistorySource HistorySource { get; }

    public ProtectedClipboardImageStore ImageStore { get; }

    public WindowsClipboardPlatform ClipboardPlatform { get; }

    public ClipboardPanelActionSink ActionSink { get; }

    public ClipboardCaptureService CaptureService { get; }

    public StartupRegistrationService StartupService { get; }

    public NativeUiSpikeViewModel CreateViewModel() =>
        NativeUiSpikeViewModel.CreateProduction(HistorySource, ActionSink, SettingsStore);
}
