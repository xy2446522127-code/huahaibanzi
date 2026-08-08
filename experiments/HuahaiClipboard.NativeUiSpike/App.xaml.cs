using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using HuahaiClipboard.App.Infrastructure.Tray;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.NativeUiSpike.Presentation.Windows;
using HuahaiClipboard.NativeUiSpike.Production;
using HuahaiClipboard.NativeUiSpike.Services;

namespace HuahaiClipboard.NativeUiSpike;

public partial class App : Application
{
    private SingleInstanceActivationService? activationService;
    private WpfGlobalInputService? inputService;
    private TrayService? trayService;
    private ProductionCompositionRoot? compositionRoot;
    private bool exitRequested;
    private bool backgroundEnabled = true;
    private MainWindow? window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        activationService = SingleInstanceActivationService.CreateOrSignal();
        if (!activationService.IsPrimary)
        {
            Shutdown(0);
            return;
        }

        compositionRoot = new ProductionCompositionRoot();
        var viewModel = compositionRoot.CreateViewModel();
        await viewModel.LoadAsync();
        await compositionRoot.HistorySource.PruneAsync(
            DateTimeOffset.Now.AddDays(-viewModel.CurrentSettings.Behavior.AutoCleanupDays),
            preserveProtected: true,
            CancellationToken.None);
        await viewModel.ReloadHistoryAsync();
        window = new MainWindow(viewModel);
        window.StartupChanged += enabled =>
        {
            compositionRoot.StartupService.SetEnabled(enabled);
            window.SetStartupState(compositionRoot.StartupService.IsEnabled());
        };
        window.SetStartupState(compositionRoot.StartupService.IsEnabled());
        var handle = new WindowInteropHelper(window).EnsureHandle();
        window.Closing += Window_Closing;
        activationService.Activated += (_, _) => Dispatcher.BeginInvoke(() =>
            ShowFromInput(WpfGlobalInputService.GetForegroundTarget(), WindowCompositionService.GetCursorPosition()));
        activationService.StartListening();

        var settings = await compositionRoot.SettingsStore.LoadAsync(CancellationToken.None);
        backgroundEnabled = settings.Behavior.BackgroundEnabled;
        var settingsSnapshot = new InputSettingsSnapshot(settings.Input);
        inputService = new WpfGlobalInputService(
            handle,
            Dispatcher,
            settingsSnapshot,
            compositionRoot.CaptureService,
            ShowFromInput);
        viewModel.SettingsChanged += (_, updated) =>
        {
            backgroundEnabled = updated.Behavior.BackgroundEnabled;
            inputService.UpdateInputSettings(updated.Input);
        };
        trayService = new TrayService(
            () => Dispatcher.BeginInvoke(() => ShowFromInput(
                WpfGlobalInputService.GetForegroundTarget(),
                WindowCompositionService.GetCursorPosition())),
            () => Dispatcher.BeginInvoke(ShowSettingsFromTray),
            () => Dispatcher.BeginInvoke(ShowSettingsFromTray),
            () => Dispatcher.BeginInvoke(ExitApplication));
        compositionRoot.CaptureService.HistoryChanged += CaptureService_HistoryChanged;

        if (e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            window.StateController.Hide();
        }
        else
        {
            ShowFromInput(WpfGlobalInputService.GetForegroundTarget(), WindowCompositionService.GetCursorPosition());
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        trayService?.Dispose();
        inputService?.Dispose();
        activationService?.Dispose();
        base.OnExit(e);
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (exitRequested) return;
        if (!backgroundEnabled)
        {
            exitRequested = true;
            Dispatcher.BeginInvoke(() => Shutdown(0));
            return;
        }
        e.Cancel = true;
        window?.StateController.Hide();
    }

    private void CaptureService_HistoryChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => _ = window?.ViewModel.ReloadHistoryAsync());

    private void ShowFromInput(IntPtr target, Point cursor)
    {
        if (window is null || compositionRoot is null) return;
        compositionRoot.ClipboardPlatform.SetPasteTarget(target);
        window.StateController.ShowAt(cursor);
    }

    private void ShowSettingsFromTray()
    {
        if (window is null) return;
        ShowFromInput(WpfGlobalInputService.GetForegroundTarget(), WindowCompositionService.GetCursorPosition());
        window.StateController.OpenSettings();
    }

    private void ExitApplication()
    {
        if (exitRequested) return;
        exitRequested = true;
        window?.Close();
        Shutdown(0);
    }
}
