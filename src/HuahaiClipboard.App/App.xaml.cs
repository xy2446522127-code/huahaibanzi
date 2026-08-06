using Microsoft.Windows.AppLifecycle;
using HuahaiClipboard.Core.Services;

namespace HuahaiClipboard.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Presentation.Windows.CursorPanelWindow? window;
    private AppInstance? mainInstance;

    public App() => InitializeComponent();

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandLineArguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));
        var startHidden = StartupLaunchPolicy.ShouldStartHidden(args.Arguments) ||
            StartupLaunchPolicy.ShouldStartHidden(commandLineArguments);
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        mainInstance = AppInstance.FindOrRegisterForKey("HuahaiClipboard.Main");
        if (!mainInstance.IsCurrent)
        {
            if (!startHidden)
            {
                await mainInstance.RedirectActivationToAsync(activation);
            }
            Exit();
            return;
        }

        mainInstance.Activated += MainInstance_Activated;
        window = new Presentation.Windows.CursorPanelWindow();
        window.Activate();
        if (startHidden)
        {
            window.StartHidden();
        }
        await window.InitializeShellAsync();
        if (startHidden)
        {
            window.StartHidden();
        }
        await window.InitializeRuntimeAsync();
        if (startHidden)
        {
            window.StartHidden();
        }
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments e) =>
        window?.DispatcherQueue.TryEnqueue(window.ShowFromShortcut);
}
