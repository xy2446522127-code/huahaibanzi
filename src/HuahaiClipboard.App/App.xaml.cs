using Microsoft.Windows.AppLifecycle;
using HuahaiClipboard.App.Infrastructure.Activation;
using HuahaiClipboard.Core.Services;

namespace HuahaiClipboard.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Presentation.Windows.CursorPanelWindow? window;
    private AppInstance? mainInstance;
    private ExternalActivationSignal? externalActivationSignal;
    private readonly DeferredActivationGate activationGate = new();

    public App() => InitializeComponent();

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var commandLineArguments = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));
        var startHidden = StartupLaunchPolicy.ShouldStartHidden(args.Arguments) ||
            StartupLaunchPolicy.ShouldStartHidden(commandLineArguments);
        mainInstance = Program.MainInstance ?? AppInstance.FindOrRegisterForKey("HuahaiClipboard.Main");

        mainInstance.Activated += MainInstance_Activated;
        externalActivationSignal = new ExternalActivationSignal(
            ExternalActivationSignal.DefaultEventName,
            RequestPanelActivation);
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
        if (activationGate.MarkReady())
        {
            window.ShowFromShortcut();
        }
    }

    private void MainInstance_Activated(object? sender, AppActivationArguments e)
        => RequestPanelActivation();

    private void RequestPanelActivation()
    {
        if (!activationGate.RequestActivation())
        {
            return;
        }

        window?.DispatcherQueue.TryEnqueue(window.ShowFromShortcut);
    }
}
