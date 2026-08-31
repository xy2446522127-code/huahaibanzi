using System.Runtime.InteropServices;
using System.Text.Json;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Todo;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace HuahaiClipboard.App.Presentation.Windows;

public sealed class TodoWorkspaceWindow : Window
{
    private const int ShowWindowHide = 0;
    private const uint WmNcLeftButtonDown = 0x00A1;
    private const int HitTestCaption = 2;
    private readonly TodoWorkspaceService workspaceService;
    private readonly Microsoft.UI.Xaml.Controls.WebView2 webView = new();
    private readonly string assetsDirectory;
    private AppWindow? appWindow;
    private bool shellReady;
    private bool topmost;
    private bool collapsed;

    public TodoWorkspaceWindow(TodoWorkspaceService workspaceService, string assetsDirectory)
    {
        this.workspaceService = workspaceService;
        this.assetsDirectory = assetsDirectory;
        Content = webView;
        Activated += (_, _) => ConfigureWindow();
        Closed += (_, _) => IsOpen = false;
    }

    public bool IsOpen { get; private set; }

    public async Task OpenAsync()
    {
        ConfigureWindow();
        IsOpen = true;
        Activate();
        await EnsureShellAsync();
        await PostStateAsync();
    }

    private void ConfigureWindow()
    {
        if (appWindow is not null) return;
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = topmost;
        }
        appWindow?.MoveAndResize(new RectInt32(80, 90, 560, 620));
    }

    private async Task EnsureShellAsync()
    {
        if (webView.CoreWebView2 is not null) return;
        await webView.EnsureCoreWebView2Async();
        var core = webView.CoreWebView2 ?? throw new InvalidOperationException("待办窗口未能初始化。");
        core.SetVirtualHostNameToFolderMapping("app.huahai.local", assetsDirectory, CoreWebView2HostResourceAccessKind.Allow);
        core.WebMessageReceived += WebMessageReceived;
        webView.Source = new Uri("https://app.huahai.local/Web/todo-shell.html");
    }

    private async void WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!WebBridgeRequest.TryParse(args.WebMessageAsJson, out var request) || request is null || !WebBridgeProtocol.IsSupported(request.Action)) return;
        switch (request.Action)
        {
            case "todoReady": shellReady = true; break;
            case "todoAdd": await workspaceService.AddTodoAsync(request.Text ?? string.Empty); break;
            case "todoToggle": if (request.Id is not null) await workspaceService.ToggleTodoAsync(request.Id); break;
            case "todoDelete": if (request.Id is not null) await workspaceService.DeleteTodoAsync(request.Id); break;
            case "todoMove": if (request.Id is not null && request.Text is not null) await workspaceService.MoveTodoAsync(request.Id, request.Text, request.Enabled == true); break;
            case "todoAddNote": await workspaceService.AddNoteAsync(request.Text ?? "新笔记"); break;
            case "todoUpdateNote": if (request.Id is not null) await workspaceService.UpdateNoteAsync(request.Id, request.Text ?? "无标题笔记", request.Mode ?? string.Empty); break;
            case "todoDeleteNote": if (request.Id is not null) await workspaceService.DeleteNoteAsync(request.Id); break;
            case "todoSetCapsule": await workspaceService.SetCapsuleEnabledAsync(request.Enabled != false); break;
            case "todoCollapse": await CollapseAsync(); return;
            case "todoRestore": Restore(); return;
            case "todoTopmost": SetTopmost(request.Enabled == true); break;
            case "todoClose": HideWindow(); return;
            case "beginNativeDrag": BeginNativeDrag(); return;
        }
        await PostStateAsync();
    }

    private async Task PostStateAsync()
    {
        if (!shellReady || webView.CoreWebView2 is null) return;
        var workspace = await workspaceService.LoadForDisplayAsync();
        webView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "todoState", workspace, topmost, collapsed }));
    }

    private void SetTopmost(bool enabled)
    {
        topmost = enabled;
        if (appWindow?.Presenter is OverlappedPresenter presenter) presenter.IsAlwaysOnTop = enabled;
    }

    private void BeginNativeDrag()
    {
        _ = ReleaseCapture();
        _ = SendMessage(WinRT.Interop.WindowNative.GetWindowHandle(this), WmNcLeftButtonDown, new IntPtr(HitTestCaption), IntPtr.Zero);
    }

    private async Task CollapseAsync()
    {
        if (appWindow is null || !(await workspaceService.LoadAsync()).CapsuleEnabled)
        {
            return;
        }

        collapsed = true;
        appWindow.Resize(new SizeInt32(300, 48));
        await PostStateAsync();
    }

    private void Restore()
    {
        if (appWindow is null)
        {
            return;
        }

        collapsed = false;
        appWindow.Resize(new SizeInt32(560, 620));
        _ = PostStateAsync();
    }


    private void HideWindow() => _ = ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowWindowHide);

    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
