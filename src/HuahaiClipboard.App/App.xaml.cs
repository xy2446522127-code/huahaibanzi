namespace HuahaiClipboard.App;

public partial class App : Microsoft.UI.Xaml.Application
{
    private Presentation.Windows.CursorPanelWindow? window;

    public App() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        window = new Presentation.Windows.CursorPanelWindow();
        window.Activate();
    }
}
