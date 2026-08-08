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
    private const int ShowWindowHide = 0;
    private const int LeftMouseButton = 0x01;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private static readonly Version CurrentVersion = new(1, 1, 8);

    private readonly CompositionRoot compositionRoot = new();
    private readonly WindowNavigator navigator = new();
    private readonly PanelViewModel panelViewModel;
    private readonly SettingsViewModel settingsViewModel;
    private readonly StartupRegistrationService startupRegistrationService = new();
    private readonly JsonWindowPlacementStore windowPlacementStore;
    private readonly TransientWindowVisibilityController visibilityController;
    private readonly GitHubUpdateCheckService updateCheckService = GitHubUpdateCheckService.CreateDefault(CurrentVersion);
    private readonly UpdateNotificationSession updateNotificationSession = new();
    private readonly UpdateStartupGate updateStartupGate = new();
    private GlobalInputService? globalInputService;
    private TrayService? trayService;
    private ProactiveUpdateCoordinator? updateCoordinator;
    private InputSettingsSnapshot? inputSettingsSnapshot;
    private AppWindow? appWindow;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? dragTimer;
    private PointInt32? dragPointerOrigin;
    private PointInt32? dragWindowOrigin;
    private RectInt32? dragWorkArea;
    private int dragWindowWidth;
    private int dragWindowHeight;
    private bool allowClose;
    private bool shellReady;
    private bool openSettingsWhenReady;
    private bool settingsSurfaceVisible;
    private PanelScalePreviewSession panelScaleSession = new(1d);
    private bool panelScalePreviewActive;
    private int webContentActivityVersion;
    private UpdateCheckResult? availableUpdate;
    private bool updateInstallationInProgress;
    private bool notifyUpdateOnNextSummon;

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
            () => DispatcherQueue.TryEnqueue(ShowUpdatePane),
            () => DispatcherQueue.TryEnqueue(ExitApplication));
        TryStartUpdateCoordinator();
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
        panelScaleSession = new PanelScalePreviewSession(settingsViewModel.Draft.Appearance.PanelScale);
        ResizeWindow(PanelWidth, PanelHeight);
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
                    TryStartUpdateCoordinator();
                    break;
                case "hide":
                    // 工具栏“隐藏”是显式后台动作，不受“关闭后退出”偏好影响。
                    visibilityController.Hide();
                    break;
                case "resize":
                    settingsSurfaceVisible = request.Mode == "settings";
                    ResizeWindow(
                        settingsSurfaceVisible ? SettingsWidth : PanelWidth,
                        settingsSurfaceVisible ? SettingsHeight : PanelHeight);
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
            case "requestThumbnail":
                await PostThumbnailAsync(FindRecord(request.Id));
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
            case "previewPanelScale":
                PreviewPanelScale(request.Number ?? 1d);
                return;
            case "cancelPanelScale":
                panelScaleSession.Cancel();
                panelScalePreviewActive = false;
                ResizeWindow(
                    settingsSurfaceVisible ? SettingsWidth : PanelWidth,
                    settingsSurfaceVisible ? SettingsHeight : PanelHeight);
                return;
            case "commitPanelScale":
            case "setPanelScale":
                var committedPanelScale = panelScaleSession.Commit(request.Number ?? 1d);
                await settingsViewModel.UpdateAppearanceAsync(
                    settingsViewModel.Draft.Appearance with { PanelScale = committedPanelScale });
                panelScalePreviewActive = false;
                ResizeWindow(
                    settingsSurfaceVisible ? SettingsWidth : PanelWidth,
                    settingsSurfaceVisible ? SettingsHeight : PanelHeight);
                break;
            case "setCheckUpdatesOnStartup":
                await settingsViewModel.UpdateBehaviorAsync(
                    settingsViewModel.Draft.Behavior with { CheckUpdatesOnStartup = request.Enabled == true });
                if (request.Enabled == true)
                {
                    TryStartUpdateCoordinator();
                    _ = CheckForUpdatesAsync();
                }
                break;
            case "checkUpdate":
                await CheckForUpdatesAsync();
                return;
            case "snoozeUpdate":
                await SnoozeUpdateAsync();
                return;
            case "installUpdate":
                await InstallUpdateAsync();
                return;
            case "openRelease":
                Process.Start(new ProcessStartInfo(GitHubUpdateCheckService.ReleasesPage)
                {
                    UseShellExecute = true
                });
                return;
            case "beginNativeDrag":
                BeginNativeWindowDrag(request.X, request.Y);
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
            history = panelViewModel.AllRecords.Select(record =>
            {
                var display = ClipboardRecordDisplay.From(record);
                return new
                {
                    id = record.Id.ToString("D"),
                    kind = KindName(record.Kind),
                    text = display.Title,
                    meta = $"{FormatRelativeTime(record.LastCopiedAt)} · {display.Detail}",
                    fav = record.IsFavorite,
                    pin = record.IsPinned,
                    available = record.IsAvailable,
                    thumbnailAvailable = display.HasThumbnail
                };
            }),
            settings = new
            {
                themeId = settings.Appearance.ThemeId,
                opacity = settings.Appearance.Opacity,
                panelScale = settings.Appearance.PanelScale,
                petalsEnabled = settings.Motion.PetalLevel != PetalLevel.Off,
                reduceMotion = settings.Motion.ReduceMotion,
                clickDuration = settings.Motion.ClickDurationMs,
                rightDoubleClick = settings.Input.RightDoubleClickEnabled,
                customShortcut = settings.Input.HotkeyEnabled ? settings.Input.CustomShortcut : null,
                exclusions = settings.Input.ExcludedApplications,
                retentionDays = settings.Behavior.AutoCleanupDays,
                backgroundEnabled = settings.Behavior.BackgroundEnabled,
                checkUpdatesOnStartup = settings.Behavior.CheckUpdatesOnStartup,
                startupEnabled = startupRegistrationService.IsEnabled(),
                dataPath = compositionRoot.DataLayout.DataDirectory
            },
            warnings = globalInputService?.InitializationWarnings ?? []
        };
        ProductWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message));
        return Task.CompletedTask;
    }

    private async Task PostThumbnailAsync(ClipboardRecord record)
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return;
        }

        var dataUrl = await compositionRoot.ImagePreviewSource.CreateDataUrlAsync(
            record,
            CancellationToken.None);
        ProductWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "thumbnail",
            id = record.Id.ToString("D"),
            dataUrl
        }));
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

    private void TryStartUpdateCoordinator()
    {
        if (!updateStartupGate.TryBegin(shellReady, trayService is not null))
        {
            return;
        }

        updateCoordinator ??= new ProactiveUpdateCoordinator(
            _ => Task.FromResult(settingsViewModel.Draft.Behavior.CheckUpdatesOnStartup),
            cancellationToken => updateCheckService.CheckAsync(cancellationToken),
            (result, _) => EnqueueUpdateAsync(() => HandleUpdateResultAsync(result, allowNotification: true)),
            (exception, _) => EnqueueUpdateAsync(() => PostUpdateStatusAsync(
                "error",
                $"后台检查暂时不可用：{exception.Message}",
                GitHubUpdateCheckService.ReleasesPage)));
        updateCoordinator.Start();
    }

    private Task EnqueueUpdateAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }))
        {
            completion.TrySetResult();
        }

        return completion.Task;
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            var result = await updateCheckService.CheckAsync(CancellationToken.None);
            await HandleUpdateResultAsync(result, allowNotification: false);
        }
        catch (Exception exception)
        {
            await PostUpdateStatusAsync(
                "error",
                $"暂时无法检查更新：{exception.Message}",
                GitHubUpdateCheckService.ReleasesPage);
        }
    }

    private async Task HandleUpdateResultAsync(UpdateCheckResult result, bool allowNotification)
    {
        availableUpdate = result.UpdateAvailable ? result : null;
        trayService?.SetUpdateAvailable(availableUpdate?.LatestVersion);

        var behavior = settingsViewModel.Draft.Behavior;
        var shouldNotify = result.UpdateAvailable &&
            allowNotification &&
            updateNotificationSession.ShouldNotify(
                result.LatestVersion,
                behavior.SnoozedUpdateVersion,
                behavior.UpdateSnoozeUntil,
                DateTimeOffset.UtcNow);
        if (shouldNotify)
        {
            updateNotificationSession.MarkNotified(result.LatestVersion);
            notifyUpdateOnNextSummon = true;
            trayService?.NotifyUpdateAvailable(result.LatestVersion);
        }

        await PostUpdateStatusAsync(
            result.UpdateAvailable ? "available" : "current",
            result.UpdateAvailable
                ? result.CanAutoInstall
                    ? $"发现新版本 {result.LatestVersion}，可以安全下载并更新。"
                    : $"发现新版本 {result.LatestVersion}。GitHub 接口暂时限流，可在网页下载，或稍后再试自动安装。"
                : $"当前已是最新版本 {result.CurrentVersion}。",
            result.ReleaseUrl,
            canInstall: result.UpdateAvailable && result.CanAutoInstall,
            notifyUser: false);
    }

    private async Task SnoozeUpdateAsync()
    {
        var release = availableUpdate
            ?? throw new InvalidOperationException("当前没有可稍后提醒的新版本。");
        var snoozedUntil = DateTimeOffset.UtcNow.Add(UpdateReminderPolicy.SnoozeDuration);
        await settingsViewModel.UpdateBehaviorAsync(
            settingsViewModel.Draft.Behavior with
            {
                SnoozedUpdateVersion = release.LatestVersion.ToString(3),
                UpdateSnoozeUntil = snoozedUntil,
            });
        updateNotificationSession.MarkSnoozed(release.LatestVersion);
        notifyUpdateOnNextSummon = false;
        await PostUpdateStatusAsync(
            "available",
            $"已稍后提醒：v{release.LatestVersion.ToString(3)} 将在 24 小时后再次提醒。",
            release.ReleaseUrl,
            canInstall: release.CanAutoInstall,
            notifyUser: false);
    }

    private async Task InstallUpdateAsync()
    {
        if (updateInstallationInProgress)
        {
            return;
        }

        var release = availableUpdate
            ?? throw new InvalidOperationException("请先检查并确认存在新版本。");
        updateInstallationInProgress = true;
        try
        {
            await PostUpdateStatusAsync(
                "downloading",
                "正在安全下载更新包… 0%",
                release.ReleaseUrl,
                progress: 0);
            var progress = new Progress<int>(value =>
            {
                _ = PostUpdateStatusAsync(
                    "downloading",
                    $"正在安全下载更新包… {value}%",
                    release.ReleaseUrl,
                    progress: value);
            });
            var updateDirectory = UpdateCacheLayout.ResolvePendingDirectory(
                Path.GetTempPath(),
                LocalDataLayout.ResolveUserKey());
            var installerPath = await updateCheckService.DownloadInstallerAsync(
                release,
                updateDirectory,
                progress,
                CancellationToken.None);
            await PostUpdateStatusAsync(
                "installing",
                "下载与 SHA-256 校验完成，正在启动安装器…",
                release.ReleaseUrl,
                progress: 100);

            using var installerProcess = UpdateInstallerLauncher.Start(
                installerPath,
                AppContext.BaseDirectory);
            ExitApplication();
        }
        catch (Exception exception)
        {
            await PostUpdateStatusAsync(
                "error",
                $"更新失败，旧版本未被替换：{exception.Message}",
                release.ReleaseUrl,
                canInstall: true);
        }
        finally
        {
            updateInstallationInProgress = false;
        }
    }

    private Task PostUpdateStatusAsync(
        string status,
        string message,
        string releaseUrl,
        bool canInstall = false,
        int? progress = null,
        bool notifyUser = false)
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        ProductWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "updateStatus",
            status,
            message,
            releaseUrl,
            canInstall,
            progress,
            updateAvailable = availableUpdate is not null,
            latestVersion = availableUpdate?.LatestVersion.ToString(3),
            notifyUser,
            snoozedUntil = settingsViewModel.Draft.Behavior.UpdateSnoozeUntil
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
        settingsSurfaceVisible = true;
        ResizeWindow(SettingsWidth, SettingsHeight);
        _ = ExecuteShellScriptAsync("document.querySelector('#settingsButton')?.click()");
    }

    private void ShowUpdatePane()
    {
        ShowAtCurrentCursor();
        ShowSettingsPane();
        _ = ExecuteShellScriptAsync(
            "document.querySelector('.nav-button[data-page=\"about\"]')?.click()");
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
        settingsSurfaceVisible = false;
        compositionRoot.ClipboardPlatform.SetPasteTarget(targetWindow);
        var display = DisplayArea.GetFromPoint(cursor, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var width = ScaleDimension(PanelWidth);
        var height = ScaleDimension(PanelHeight);
        var x = cursor.X + 14;
        if (x + width > workArea.X + workArea.Width)
        {
            x = cursor.X - width - 14;
        }

        x = Math.Clamp(x, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - width));
        var y = Math.Clamp(cursor.Y - 48, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - height));
        appWindow?.MoveAndResize(new RectInt32(x, y, width, height));
        ApplyNativeGlassChrome(
            WinRT.Interop.WindowNative.GetWindowHandle(this),
            width,
            height);
        visibilityController.Show();
        Activate();
        _ = SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        _ = ExecuteShellScriptAsync(
            "document.querySelector('#glassPanel')?.classList.remove('hidden','settings-mode');document.querySelector('#petals')?.classList.remove('paused');document.querySelector('#searchInput')?.focus()");
        if (notifyUpdateOnNextSummon && availableUpdate is { } release)
        {
            notifyUpdateOnNextSummon = false;
            _ = PostUpdateStatusAsync(
                "available",
                $"发现新版本 {release.LatestVersion}，可在“关于与更新”中查看。",
                release.ReleaseUrl,
                canInstall: release.CanAutoInstall,
                notifyUser: true);
        }
    }

    private async Task ExecuteShellScriptAsync(string script)
    {
        if (!shellReady || ProductWebView.CoreWebView2 is null)
        {
            return;
        }

        await ProductWebView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void PreviewPanelScale(double ratio)
    {
        panelScaleSession.Preview(ratio);
        if (!panelScalePreviewActive)
        {
            panelScalePreviewActive = true;
            _ = SetWindowRgn(WinRT.Interop.WindowNative.GetWindowHandle(this), IntPtr.Zero, false);
        }

        ResizeWindow(
            settingsSurfaceVisible ? SettingsWidth : PanelWidth,
            settingsSurfaceVisible ? SettingsHeight : PanelHeight,
            rebuildChrome: false);
    }

    private void ResizeWindow(int width, int height, bool rebuildChrome = true)
    {
        if (appWindow is null)
        {
            return;
        }

        width = ScaleDimension(width);
        height = ScaleDimension(height);
        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var workArea = display.WorkArea;
        var current = appWindow.Position;
        var x = Math.Clamp(current.X, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - width));
        var y = Math.Clamp(current.Y, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - height));
        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
        if (rebuildChrome)
        {
            ApplyNativeGlassChrome(
                WinRT.Interop.WindowNative.GetWindowHandle(this),
                width,
                height);
        }
    }

    private int ScaleDimension(int value) =>
        Math.Max(1, (int)Math.Round(value * panelScaleSession.CurrentRatio));

    private void BeginNativeWindowDrag(double? pointerX, double? pointerY)
    {
        if (appWindow is null)
        {
            return;
        }

        if (pointerX is not null && pointerY is not null)
        {
            dragPointerOrigin = new PointInt32(
                (int)Math.Round(pointerX.Value),
                (int)Math.Round(pointerY.Value));
        }
        else if (GetCursorPos(out var cursor))
        {
            dragPointerOrigin = new PointInt32(cursor.X, cursor.Y);
        }
        else
        {
            return;
        }
        dragWindowOrigin = appWindow.Position;
        dragWindowWidth = ScaleDimension(settingsSurfaceVisible ? SettingsWidth : PanelWidth);
        dragWindowHeight = ScaleDimension(settingsSurfaceVisible ? SettingsHeight : PanelHeight);
        dragWorkArea = DisplayArea.GetFromPoint(
            dragPointerOrigin.Value,
            DisplayAreaFallback.Nearest).WorkArea;

        if (dragTimer is null)
        {
            dragTimer = DispatcherQueue.CreateTimer();
            dragTimer.Interval = TimeSpan.FromMilliseconds(8);
            dragTimer.IsRepeating = true;
            dragTimer.Tick += DragTimer_Tick;
        }
        dragTimer.Start();
    }

    private void DragTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object args)
    {
        if (appWindow is null ||
            dragPointerOrigin is not { } pointerOrigin ||
            dragWindowOrigin is not { } windowOrigin ||
            (GetAsyncKeyState(LeftMouseButton) & 0x8000) == 0 ||
            !GetCursorPos(out var cursor))
        {
            StopNativeWindowDrag(savePosition: true);
            return;
        }

        var pointer = new PointInt32(cursor.X, cursor.Y);
        if (dragWorkArea is not { } workArea ||
            pointer.X < workArea.X || pointer.X >= workArea.X + workArea.Width ||
            pointer.Y < workArea.Y || pointer.Y >= workArea.Y + workArea.Height)
        {
            workArea = DisplayArea.GetFromPoint(pointer, DisplayAreaFallback.Nearest).WorkArea;
            dragWorkArea = workArea;
        }

        var targetX = windowOrigin.X + pointer.X - pointerOrigin.X;
        var targetY = windowOrigin.Y + pointer.Y - pointerOrigin.Y;
        appWindow.Move(new PointInt32(
            Math.Clamp(targetX, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - dragWindowWidth)),
            Math.Clamp(targetY, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - dragWindowHeight))));
    }

    private void StopNativeWindowDrag(bool savePosition)
    {
        dragTimer?.Stop();
        var hadActiveDrag = dragPointerOrigin is not null;
        dragPointerOrigin = null;
        dragWindowOrigin = null;
        dragWorkArea = null;
        if (savePosition && hadActiveDrag)
        {
            _ = UnmanagedCallbackGuard.InvokeAsync(SaveWindowPlacementAsync);
        }
    }

    private async Task SaveWindowPlacementAsync()
    {
        if (appWindow is null)
        {
            return;
        }

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

        var width = ScaleDimension(PanelWidth);
        var height = ScaleDimension(PanelHeight);
        var display = DisplayArea.GetFromPoint(
            new PointInt32(placement.X + width / 2, placement.Y + height / 2),
            DisplayAreaFallback.Nearest);
        if (!string.Equals(DisplayKey(display), placement.DisplayId, StringComparison.Ordinal))
        {
            return;
        }

        var area = display.WorkArea;
        appWindow.Move(new PointInt32(
            Math.Clamp(placement.X, area.X, Math.Max(area.X, area.X + area.Width - width)),
            Math.Clamp(placement.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - height))));
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
        StopNativeWindowDrag(savePosition: false);
        if (dragTimer is not null)
        {
            dragTimer.Tick -= DragTimer_Tick;
            dragTimer = null;
        }
        compositionRoot.CaptureService.HistoryChanged -= CaptureService_HistoryChanged;
        globalInputService?.Dispose();
        globalInputService = null;
        _ = updateCoordinator?.DisposeAsync();
        updateCoordinator = null;
        trayService?.Dispose();
        trayService = null;
    }

    void ITransientWindowHost.SetTopmost(bool enabled)
    {
        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = enabled;
        }

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

    void ITransientWindowHost.Hide()
    {
        if (appWindow is not null)
        {
            appWindow.Hide();
            return;
        }

        _ = ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowWindowHide);
    }

    private void ApplyNativeGlassChrome(IntPtr windowHandle, int width, int height)
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

        var geometry = WindowChromeGeometry.CreateForWebView(
            width,
            height,
            PanelCornerRadius,
            GetDpiForWindow(windowHandle),
            panelScaleSession.CurrentRatio);
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
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

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
