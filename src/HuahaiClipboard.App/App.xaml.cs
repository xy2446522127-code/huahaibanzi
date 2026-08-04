using Microsoft.Windows.AppLifecycle;

namespace HuahaiClipboard.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Presentation.Windows.CursorPanelWindow? window;
    private AppInstance? mainInstance;

    public App() => InitializeComponent();

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        mainInstance = AppInstance.FindOrRegisterForKey("HuahaiClipboard.Main");
        if (!mainInstance.IsCurrent)
        {
            await mainInstance.RedirectActivationToAsync(activation);
            Exit();
            return;
        }

        mainInstance.Activated += MainInstance_Activated;
        window = new Presentation.Windows.CursorPanelWindow();
        window.Activate();
        await window.InitializeRuntimeAsync();
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments e) =>
        window?.DispatcherQueue.TryEnqueue(window.ShowFromShortcut);
}
