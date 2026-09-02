using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.App.Infrastructure.Storage;
using HuahaiClipboard.App.Presentation.Windows;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Todo;
using HuahaiClipboard.Core.Recovery;

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
    private readonly ClipboardRetentionService retentionService;
    private readonly TodoWorkspaceService todoWorkspaceService;

    public CompositionRoot()
    {
        dataLayout = ResolveDataLayout();
        var protector = new DpapiTextProtector();
        settingsStore = new JsonSettingsStore(dataLayout.SettingsFile);
        imageStore = new ProtectedClipboardImageStore(dataLayout.ImageDirectory, protector);
        historySource = new JsonClipboardHistorySource(
            dataLayout.HistoryFile,
            protector,
            imageStore);
        retentionService = new ClipboardRetentionService(historySource);
        imagePreviewSource = new ClipboardImagePreviewSourceService(imageStore);
        clipboardWriteOriginGuard = new WindowsClipboardWriteOriginGuard();
        clipboardPlatform = new WindowsClipboardPlatform(imageStore, clipboardWriteOriginGuard);
        actionSink = new ClipboardPanelActionSink(historySource, clipboardPlatform);
        captureService = new ClipboardCaptureService(
            historySource,
            settingsStore,
            imageStore,
            clipboardWriteOriginGuard);
        todoWorkspaceService = new TodoWorkspaceService(
            new JsonTodoWorkspaceStore(dataLayout.TodoWorkspaceFile),
            new TodoNoteImageRewriter(new TodoImageStore(dataLayout.TodoImageDirectory)));
    }

    private static LocalDataLayout ResolveDataLayout()
    {
        var installRoot = LocalDataLayout.ResolveInstallRoot();
        var userKey = LocalDataLayout.ResolveUserKey();
        var legacyRoot = LocalDataLayout.ResolveLegacyDataDirectory();
        IDataLocationRegistry? registry = OperatingSystem.IsWindows()
            ? new WindowsRegistryDataLocationRegistry()
            : null;

        var resolution = LocalDataLayout.ResolveDataRootAsync(
                registry ?? new NullDataLocationRegistry(),
                installRoot,
                legacyRoot)
            .GetAwaiter()
            .GetResult();

        if (resolution.Kind == DataRootResolutionKind.RecoveryRequired)
        {
            throw new DataRootRecoveryRequiredException(resolution.ConflictingDataRoots);
        }

        var layout = resolution.Kind == DataRootResolutionKind.Registered
            ? LocalDataLayout.FromDataRoot(resolution.DataRoot!, userKey)
            : new LocalDataLayout(installRoot, userKey);

        if (resolution.LegacyMigrationSource is not null)
        {
            LocalDataMigrator.MigrateIfNeeded(layout, resolution.LegacyMigrationSource);
        }

        if (registry is not null && resolution.Kind != DataRootResolutionKind.Registered)
        {
            registry.WriteAsync(layout.DataRoot, CancellationToken.None).GetAwaiter().GetResult();
        }

        return layout;
    }

    private sealed class NullDataLocationRegistry : IDataLocationRegistry
    {
        public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task WriteAsync(string dataRoot, CancellationToken cancellationToken) => Task.CompletedTask;
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

    public ClipboardRetentionService RetentionService => retentionService;

    public TodoWorkspaceService TodoWorkspaceService => todoWorkspaceService;
}
