using HuahaiClipboard.Core.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace HuahaiClipboard.App;

public static class Program
{
    internal static AppInstance? MainInstance { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var commandLineArguments = string.Join(' ', args);
        var startHidden = StartupLaunchPolicy.ShouldStartHidden(commandLineArguments);
        var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
        var instance = AppInstance.FindOrRegisterForKey("HuahaiClipboard.Main");
        if (!instance.IsCurrent)
        {
            if (!startHidden)
            {
                instance.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult();
            }
            return;
        }

        MainInstance = instance;
        Application.Start(_ =>
        {
            var context = new DispatcherQueueSynchronizationContext(
                DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
