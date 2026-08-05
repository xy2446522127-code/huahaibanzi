using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using HuahaiClipboard.App.Infrastructure.Input;
using HuahaiClipboard.App.Infrastructure.Startup;
using HuahaiClipboard.App.Infrastructure.Tray;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace HuahaiClipboard.App.Presentation.Windows;

public sealed partial class CursorPanelWindow : Window, ITransientWindowHost
{
    private const int PanelWidth = 430;
    private const int PanelHeight = 680;
    private const int SettingsWidth = 820;
    private const int SettingsHeight = 650;
    private const int PanelCornerRadius = 29;
    private const int DwmWindowCornerPreference = 33;
    private const int DwmBorderColor = 34;
    private const int DwmRoundCornerPreference = 2;
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);
    private const int WindowStyleIndex = -16;
    private const int ExtendedWindowStyleIndex = -20;
    private const uint SetWindowFrameChanged = 0x0020;
    private const uint SetWindowNoSize = 0x0001;
    private const uint SetWindowNoMove = 0x0002;
    private const uint SetWindowNoZOrder = 0x0004;
    private const uint SetWindowNoActivate = 0x0010;
    private const uint WmNonClientLeftButtonDown = 0x00A1;
    private const int HitTestCaption = 2;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    private readonly CompositionRoot compositionRoot = new();
    private readonly WindowNavigator navigator = new();
    private readonly PanelViewModel panelViewModel;
    private readonly SettingsViewModel settingsViewModel;
    private readonly StartupRegistrationService startupRegistrationService = new();
    private readonly JsonWindowPlacementStore windowPlacementStore;
    private readonly TransientWindowVisibilityController visibilityController;
    private GlobalInputService? globalInputService;
    private TrayService? trayService;
    private InputSettingsSnapshot? inputSettingsSnapshot;
    private AppWindow? appWindow;
    private bool allowClose;
    private bool shellReady;
    private bool openSettingsWhenReady;
    private int webContentActivityVersion;

    public CursorPanelWindow()
    {
        panelViewModel = compositionRoot.CreatePanel(navigator);
        settingsViewModel = compositionRoot.CreateSettings();
        windowPlacementStore = new JsonWindowPlacementStore(
            compositionRoot.DataLayout.WindowPositionsFile);
        visibilityController = new TransientWindowVisibilityController(this);
        InitializeComponent();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        navigator.HideTransientPanelAction = HideTransientPanel;
        navigator.SettingsAction = ShowSettingsPane;
        Closed += (_, _) => DisposeRuntime();
    }

    public async Task InitializeRuntimeAsync()
    {
        if (globalInputService is not null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var settings = await compositionRoot.SettingsStore.LoadAsync(CancellationToken.None);
        await compositionRoot.HistorySource.PruneAsync(
            DateTimeOffset.Now.AddDays(-settings.Behavior.AutoCleanupDays),
            preserveProtected: true,
            CancellationToken.None);
        await panelViewModel.LoadAsync();
        await UnmanagedCallbackGuard.InvokeAsync(
            () => compositionRoot.ImageStore.ProtectLegacyFilesAsync(CancellationToken.None));
        inputSettingsSnapshot = new InputSettingsSnapshot(settings.Input);
        compositionRoot.CaptureService.HistoryChanged += CaptureService_HistoryChanged;
        globalInputService = new GlobalInputService(
            handle,
            DispatcherQueue,
            inputSettingsSnapshot,
            compositionRoot.CaptureService,
            ShowAtCursor);
        trayService = new TrayService(
            () => DispatcherQueue.TryEnqueue(ShowAtCurrentCursor),
            () => DispatcherQueue.TryEnqueue(() =>
            {
                ShowAtCurrentCursor();
                ShowSettingsPane();
            }),
            () => DispatcherQueue.TryEnqueue(ExitApplication));
        await PostShellStateAsync();
    }

    public void ShowFromShortcut() => ShowAtCurrentCursor();

    public void StartHidden()
    {
        visibilityController.Hide();
    }

    public async Task InitializeShellAsync()
    {
        await WaitForWebViewLayoutAsync();
        ConfigureWindow();
        await RestoreWindowPositionAsync();
        await panelViewModel.LoadAsync();
        await settingsViewModel.LoadAsync();
        await ProductWebView.EnsureCoreWebView2Async();
        ProductWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.huahai.local",
            Path.Combine(AppContext.BaseDirectory, "Assets"),
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        ProductWebView.CoreWebView2.WebMessageReceived += ProductWebView_WebMessageReceived;
        ProductWebView.Source = new Uri("https://app.huahai.local/Web/product-shell.html");
    }

    private Task WaitForWebViewLayoutAsync()
    {
        if (ProductWebView.XamlRoot is not null)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler? loaded = null;
        loaded = (_, _) =>
        {
            ProductWebView.Loaded -= loaded;
            completion.TrySetResult();
        };
        ProductWebView.Loaded += loaded;
        return completion.Task;
    }

    private void ConfigureWindow()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(handle);
        appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow?.Resize(new SizeInt32(PanelWidth, PanelHeight));
        if (appWindow is null)
        {
            return;
        }

        appWindow.Closing += AppWindow_Closing;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        ApplyNativeGlassChrome(handle, PanelWidth, PanelHeight);
        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        appWindow.Move(new PointInt32(
            workArea.X + workArea.Width - PanelWidth - 16,
            workArea.Y + 24));
    }

    private async void ProductWebView_WebMessageReceived(
        Microsoft.Web.WebView2.Core.CoreWebView2 sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        try
        {
            if (!WebBridgeRequest.TryParse(args.WebMessageAsJson, out var request) ||
                request is null ||
                !WebBridgeProtocol.IsSupported(request.Action))
            {
                return;
            }

            switch (request.Action)
            {
                case "ready":
                    shellReady = true;
                    await PostShellStateAsync();
                    if (openSettingsWhenReady)
                    {
                        await ExecuteShellScriptAsync("document.querySelector('#settingsButton')?.click()");
                    }
                    break;
                case "hide":
                    // 工具栏“隐藏”是显式后台动作，不受“关闭后退出”偏好影响。
                    visibilityController.Hide();
                    break;
                case "resize":
                    ResizeWindow(
                        request.Mode == "settings" ? SettingsWidth : PanelWidth,
                        request.Mode == "settings" ? SettingsHeight : PanelHeight);
                    break;
                default:
                    await HandleShellActionAsync(request);
                    break;
            }
        }
        catch (Exception exception)
        {
            await PostShellToastAsync(
                string.IsNullOrWhiteSpace(exception.Message) ? "操作失败，请重试" : exception.Message,
                isError: true);
        }
    }

    private async Task HandleShellActionAsync(WebBridgeRequest request)
    {
        switch (request.Action)
        {
            case "copy":
                var copyRecord = FindRecord(request.Id);
                var copyResult = await compositionRoot.ActionSink.CopyAsync(
                    copyRecord.Id,
                    CancellationToken.None);
                if (!copyResult.Succeeded)
                {
                    await PostShellToastAsync(copyResult.RecoveryMessage ?? "复制失败", isError: true);
                    return;
                }

                if (request.Enabled != false)
                {
                    HideTransientPanel();
                }
                return;
            case "togglePin":
                await panelViewModel.TogglePinnedAsync(FindRecord(request.Id));
                break;
            case "toggleFavorite":
                await panelViewModel.ToggleFavoriteAsync(FindRecord(request.Id));
                break;
            case "delete":
                await panelViewModel.DeleteAsync(FindRecord(request.Id));
                break;
            case "setRetentionDays":
                var retentionDays = NormalizeRetentionDays(request.Number);
                await settingsViewModel.UpdateBehaviorAsync(
                    settingsViewModel.Draft.Behavior with { AutoCleanupDays = retentionDays });
                await compositionRoot.HistorySource.PruneAsync(
                    DateTimeOffset.Now.AddDays(-retentionDays),
                    preserveProtected: true,
                    CancellationToken.None);
                await panelViewModel.LoadAsync();
                break;
            case "clearOrdinary":
                await compositionRoot.HistorySource.ClearUnprotectedAsync(CancellationToken.None);
                await panelViewModel.LoadAsync();
                break;
            case "clearAll":
                await compositionRoot.HistorySource.ClearAsync(CancellationToken.None);
                await panelViewModel.LoadAsync();
                break;
            case "setTheme":
                await settingsViewModel.UpdateAppearanceAsync(
                    settingsViewModel.Draft.Appearance with { ThemeId = NormalizeThemeId(request.Text) });
                break;
            case "setOpacity":
                await settingsViewModel.UpdateAppearanceAsync(
                    settingsViewModel.Draft.Appearance with
                    {
                        Opacity = Math.Clamp(request.Number ?? 0.88, 0.65, 0.96)
                    });
                break;
            case "setPetals":
                await settingsViewModel.UpdateMotionAsync(
                    settingsViewModel.Draft.Motion with
                    {
                        PetalLevel = request.Enabled == false ? PetalLevel.Off : PetalLevel.Low
                    });
                break;
            case "setReduceMotion":
                await settingsViewModel.UpdateMotionAsync(
                    settingsViewModel.Draft.Motion with { ReduceMotion = request.Enabled == true });
                break;
            case "setClickDuration":
                await settingsViewModel.UpdateMotionAsync(
                    settingsViewModel.Draft.Motion with
                    {
                        ClickDurationMs = Math.Clamp((int)(request.Number ?? 620), 180, 900)
                    });
                break;
            case "setRightDoubleClick":
                await SaveInputSettingsAsync(
                    settingsViewModel.Draft.Input with { RightDoubleClickEnabled = request.Enabled == true });
                break;
            case "setShortcut":
                if (!ShortcutGestureParser.TryParse(request.Text, out _))
                {
                    throw new InvalidOperationException("该按键组合不能用作全局快捷唤出");
                }
                await SaveInputSettingsAsync(
                    settingsViewModel.Draft.Input with
                    {
                        HotkeyEnabled = true,
                        CustomShortcut = request.Text
                    });
                break;
            case "resetShortcut":
                await SaveInputSettingsAsync(
                    settingsViewModel.Draft.Input with
                    {
                        HotkeyEnabled = false,
                        CustomShortcut = null
                    });
                break;
            case "setExclusions":
                await SaveInputSettingsAsync(
                    settingsViewModel.Draft.Input with
                    {
                        ExcludedApplications = request.Values
                            .Select(value => value.Trim())
                            .Where(value => value.Length > 0)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToArray()
                    });
                break;
            case "openDataFolder":
                Directory.CreateDirectory(compositionRoot.DataLayout.DataDirectory);
                Process.Start(new ProcessStartInfo(compositionRoot.DataLayout.DataDirectory)
                {
                    UseShellExecute = true
                });
                return;
            case "setStartup":
                startupRegistrationService.SetEnabled(request.Enabled == true);
                break;
            case "setBackground":
                await settingsViewModel.UpdateBehaviorAsync(
                    settingsViewModel.Draft.Behavior with { BackgroundEnabled = request.Enabled == true });
                break;
            case "beginDrag":
                await BeginWindowDragAsync();
                return;
        }

        await PostShellStateAsync();
    }

    private ClipboardRecord FindRecord(string? id)
    {
        if (!Guid.TryParse(id, out var recordId))
        {
            throw new InvalidOperationException("记录标识无效");
        }

        return panelViewModel.AllRecords.FirstOrDefault(record => record.Id == recordId)
            ?? throw new InvalidOperationException("记录不存在或已删除");
    }

    private async Task SaveInputSettingsAsync(InputSettings input)
    {
        await settingsViewModel.UpdateInputAsync(input);
        globalInputService?.UpdateInputSettings(input);
    }

    private Task PostShellStateAsync()
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        var settings = settingsViewModel.Draft;
        var message = new
        {
            type = "state",
            history = panelViewModel.AllRecords.Select(record => new
            {
                id = record.Id.ToString("D"),
                kind = KindName(record.Kind),
                text = record.PrimaryText,
                meta = $"{FormatRelativeTime(record.LastCopiedAt)} · {record.SecondaryText}",
                fav = record.IsFavorite,
                pin = record.IsPinned,
                available = record.IsAvailable
            }),
            settings = new
            {
                themeId = settings.Appearance.ThemeId,
                opacity = settings.Appearance.Opacity,
                petalsEnabled = settings.Motion.PetalLevel != PetalLevel.Off,
                reduceMotion = settings.Motion.ReduceMotion,
                clickDuration = settings.Motion.ClickDurationMs,
                rightDoubleClick = settings.Input.RightDoubleClickEnabled,
                customShortcut = settings.Input.HotkeyEnabled ? settings.Input.CustomShortcut : null,
                exclusions = settings.Input.ExcludedApplications,
                retentionDays = settings.Behavior.AutoCleanupDays,
                backgroundEnabled = settings.Behavior.BackgroundEnabled,
                startupEnabled = startupRegistrationService.IsEnabled(),
                dataPath = compositionRoot.DataLayout.DataDirectory
            },
            warnings = globalInputService?.InitializationWarnings ?? []
        };
        ProductWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
        return Task.CompletedTask;
    }

    private Task PostShellToastAsync(string message, bool isError)
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        ProductWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "toast",
            message,
            isError
        }));
        return Task.CompletedTask;
    }

    private static int NormalizeRetentionDays(double? value) => (int?)value is 3 or 7 or 30
        ? (int)value!.Value
        : throw new InvalidOperationException("自动清理期限无效");

    private static string NormalizeThemeId(string? value) => value switch
    {
        "rose-purple" or "cobalt-blue" or "emerald-cyan" or "amber-orange" or "aurora-cyan-purple" => value,
        _ => throw new InvalidOperationException("主题不存在")
    };

    private static string KindName(ClipboardItemKind kind) => kind switch
    {
        ClipboardItemKind.Text => "文本",
        ClipboardItemKind.Link => "链接",
        ClipboardItemKind.Image => "图片",
        ClipboardItemKind.File => "文件",
        _ => "文本"
    };

    private static string FormatRelativeTime(DateTimeOffset value)
    {
        var elapsed = DateTimeOffset.Now - value;
        if (elapsed.TotalMinutes < 1) return "刚刚";
        if (elapsed.TotalHours < 1) return $"{Math.Max(1, (int)elapsed.TotalMinutes)} 分钟前";
        if (elapsed.TotalDays < 1) return $"{Math.Max(1, (int)elapsed.TotalHours)} 小时前";
        if (elapsed.TotalDays < 30) return $"{Math.Max(1, (int)elapsed.TotalDays)} 天前";
        return value.ToLocalTime().ToString("yyyy-MM-dd");
    }

    private void ShowSettingsPane()
    {
        openSettingsWhenReady = true;
        ResizeWindow(SettingsWidth, SettingsHeight);
        _ = ExecuteShellScriptAsync("document.querySelector('#settingsButton')?.click()");
    }

    private void HideTransientPanel()
    {
        if (settingsViewModel.Draft.Behavior.BackgroundEnabled)
        {
            visibilityController.Hide();
            return;
        }

        ExitApplication();
    }

    private void CaptureService_HistoryChanged(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            await panelViewModel.LoadAsync();
            await PostShellStateAsync();
        });

    private void ShowAtCurrentCursor()
    {
        if (GetCursorPos(out var point))
        {
            ShowAtCursor(IntPtr.Zero, new PointInt32(point.X, point.Y));
        }
    }

    private void ShowAtCursor(IntPtr targetWindow, PointInt32 cursor)
    {
        compositionRoot.ClipboardPlatform.SetPasteTarget(targetWindow);
        var display = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var x = cursor.X + 14;
        if (x + PanelWidth > workArea.X + workArea.Width)
        {
            x = cursor.X - PanelWidth - 14;
        }

        x = Math.Clamp(x, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - PanelWidth));
        var y = Math.Clamp(cursor.Y - 48, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - PanelHeight));
        appWindow?.MoveAndResize(new RectInt32(x, y, PanelWidth, PanelHeight));
        ApplyNativeGlassChrome(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            PanelWidth,
            PanelHeight);
        visibilityController.Show();
        Activate();
        _ = SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _ = ExecuteShellScriptAsync(
            "document.querySelector('#glassPanel')?.classList.remove('hidden','settings-mode');document.querySelector('#petals')?.classList.remove('paused');document.querySelector('#searchInput')?.focus()");
    }

    private async Task ExecuteShellScriptAsync(string script)
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return;
        }

        await ProductWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void ResizeWindow(int width, int height)
    {
        if (appWindow is null)
        {
            return;
        }

        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var current = appWindow.Position;
        var x = Math.Clamp(current.X, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - width));
        var y = Math.Clamp(current.Y, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - height));
        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        ApplyNativeGlassChrome(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            width,
            height);
    }

    private async Task BeginWindowDragAsync()
    {
        if (appWindow is null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = ReleaseCapture();
        _ = SendMessage(
            handle,
            WmNonClientLeftButtonDown,
            new IntPtr(HitTestCaption),
            IntPtr.Zero);
        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        await windowPlacementStore.SaveAsync(new WindowPlacement(
            DisplayKey(display),
            appWindow.Position.X,
            appWindow.Position.Y));
    }

    private async Task RestoreWindowPositionAsync()
    {
        if (appWindow is null)
        {
            return;
        }

        var placement = await windowPlacementStore.LoadLastAsync();
        if (placement is null)
        {
            return;
        }

        var display = DisplayArea.GetFromPoint(
            new PointInt32(placement.X + PanelWidth / 2, placement.Y + PanelHeight / 2),
            DisplayAreaFallback.Nearest);
        if (!string.Equals(DisplayKey(display), placement.DisplayId, StringComparison.Ordinal))
        {
            return;
        }

        var area = display.WorkArea;
        appWindow.Move(new PointInt32(
            Math.Clamp(placement.X, area.X, Math.Max(area.X, area.X + area.Width - PanelWidth)),
            Math.Clamp(placement.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - PanelHeight))));
    }

    private static string DisplayKey(DisplayArea display) =>
        display.DisplayId.Value.ToString("X16");

    private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (allowClose)
        {
            return;
        }

        args.Cancel = true;
        visibilityController.Hide();
    }

    private void ExitApplication()
    {
        allowClose = true;
        ((ITransientWindowHost)this).SetTopmost(false);
        DisposeRuntime();
        Close();
        Application.Current.Exit();
    }

    private void DisposeRuntime()
    {
        compositionRoot.CaptureService.HistoryChanged -= CaptureService_HistoryChanged;
        globalInputService?.Dispose();
        globalInputService = null;
        trayService?.Dispose();
        trayService = null;
    }

    void ITransientWindowHost.SetTopmost(bool enabled)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = SetWindowPos(
            handle,
            enabled ? HwndTopmost : HwndNoTopmost,
            0,
            0,
            0,
            0,
            SetWindowNoSize |
            SetWindowNoMove |
            (enabled ? 0u : SetWindowNoActivate));
    }

    void ITransientWindowHost.SetContentActive(bool active)
    {
        var version = Interlocked.Increment(ref webContentActivityVersion);
        var coreWebView = ProductWebView.CoreWebView2;
        if (coreWebView is null)
        {
            return;
        }

        if (active)
        {
            if (coreWebView.IsSuspended)
            {
                coreWebView.Resume();
            }

            return;
        }

        _ = SuspendWebContentAsync(version);
    }

    private async Task SuspendWebContentAsync(int version)
    {
        await Task.Yield();
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        if (version != Volatile.Read(ref webContentActivityVersion) || IsWindowVisible(handle))
        {
            return;
        }

        var coreWebView = ProductWebView.CoreWebView2;
        if (coreWebView is null || coreWebView.IsSuspended)
        {
            return;
        }

        try
        {
            _ = await coreWebView.TrySuspendAsync();
            if (version != Volatile.Read(ref webContentActivityVersion) || IsWindowVisible(handle))
            {
                coreWebView.Resume();
            }
        }
        catch (COMException)
        {
            // The window may become visible while WebView2 is processing the suspend request.
        }
    }

    void ITransientWindowHost.Show() => appWindow?.Show();

    void ITransientWindowHost.Hide() => appWindow?.Hide();

    private static void ApplyNativeGlassChrome(IntPtr windowHandle, int width, int height)
    {
        RemoveNativeWindowFrame(windowHandle);
        var cornerPreference = DwmRoundCornerPreference;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmWindowCornerPreference,
            ref cornerPreference,
            sizeof(int));
        var borderColor = DwmColorNone;
        _ = DwmSetWindowAttribute(
            windowHandle,
            DwmBorderColor,
            ref borderColor,
            sizeof(int));

        var geometry = WindowChromeGeometry.Create(
            width,
            height,
            PanelCornerRadius,
            GetDpiForWindow(windowHandle));
        var region = CreateRoundRectRgn(
            0,
            0,
            geometry.Width + 1,
            geometry.Height + 1,
            geometry.CornerDiameter,
            geometry.CornerDiameter);
        if (region != IntPtr.Zero && SetWindowRgn(windowHandle, region, true) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    private static void RemoveNativeWindowFrame(IntPtr windowHandle)
    {
        var styles = WindowChromeStyle.WithoutNativeFrame(
            GetWindowLongPtr(windowHandle, WindowStyleIndex).ToInt64(),
            GetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex).ToInt64());
        _ = SetWindowLongPtr(windowHandle, WindowStyleIndex, new IntPtr(styles.Style));
        _ = SetWindowLongPtr(windowHandle, ExtendedWindowStyleIndex, new IntPtr(styles.ExtendedStyle));
        _ = SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SetWindowFrameChanged |
            SetWindowNoSize |
            SetWindowNoMove |
            SetWindowNoZOrder |
            SetWindowNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr regionHandle, bool redraw);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(
        int left,
        int top,
        int right,
        int bottom,
        int widthEllipse,
        int heightEllipse);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int value,
        int valueSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
