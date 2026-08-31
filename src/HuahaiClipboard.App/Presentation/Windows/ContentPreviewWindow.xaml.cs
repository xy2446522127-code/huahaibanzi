using System.Runtime.InteropServices;
using System.Text.Json;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace HuahaiClipboard.App.Presentation.Windows;

public sealed class ContentPreviewWindow : Window
{
    private const int ShowWindowHide = 0;
    private const uint WmNcLeftButtonDown = 0x00A1;
    private const int HitTestCaption = 2;
    private static readonly TimeSpan AutoHideDelay = TimeSpan.FromMilliseconds(250);

    private readonly Func<Guid, PreviewEdit, Task<PreviewEditResult>> saveAsync;
    private readonly Func<Guid, Task> copyAsync;
    private readonly Func<ClipboardRecord, CancellationToken, Task<string?>> thumbnailAsync;
    private readonly Func<(string ThemeId, double Opacity)> styleProvider;
    private readonly Action<ClipboardRecord> saved;
    private readonly PreviewWindowPlacementStore placementStore;
    private readonly string assetsDirectory;
    private readonly WebView2 previewWebView = new();
    private ClipboardRecord? record;
    private ClipboardRecord? pendingRecord;
    private RectInt32 pendingBounds;
    private AppWindow? appWindow;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? autoHideTimer;
    private bool shellReady;
    private bool dirty;
    private bool editorFocused;
    private bool pointerInside;
    private bool autoHideEnabled = true;
    private bool topmost = true;

    public ContentPreviewWindow(
        Func<Guid, PreviewEdit, Task<PreviewEditResult>> saveAsync,
        Func<Guid, Task> copyAsync,
        Func<ClipboardRecord, CancellationToken, Task<string?>> thumbnailAsync,
        Func<(string ThemeId, double Opacity)> styleProvider,
        Action<ClipboardRecord> saved,
        PreviewWindowPlacementStore placementStore,
        string assetsDirectory)
    {
        this.saveAsync = saveAsync;
        this.copyAsync = copyAsync;
        this.thumbnailAsync = thumbnailAsync;
        this.styleProvider = styleProvider;
        this.saved = saved;
        this.placementStore = placementStore;
        this.assetsDirectory = assetsDirectory;
        Content = previewWebView;
        Activated += (_, _) => ConfigureWindow();
        Closed += (_, _) => IsOpen = false;
    }

    public bool IsOpen { get; private set; }

    public async Task OpenAsync(ClipboardRecord value, RectInt32 initialBounds)
    {
        if (record is not null && dirty && record.Id != value.Id)
        {
            pendingRecord = value;
            pendingBounds = initialBounds;
            await PostConfirmationAsync("switch");
            return;
        }

        await OpenRecordAsync(value, initialBounds);
    }

    private async Task OpenRecordAsync(ClipboardRecord value, RectInt32 initialBounds)
    {
        record = value;
        pendingRecord = null;
        dirty = false;
        editorFocused = false;
        pointerInside = false;
        ConfigureWindow();
        await RestorePlacementAsync(initialBounds);
        IsOpen = true;
        Activate();
        await EnsureShellAsync();
        await PostStateAsync();
    }

    private void ConfigureWindow()
    {
        if (appWindow is not null)
        {
            return;
        }

        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle));
        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsAlwaysOnTop = topmost;
        }

        if (appWindow is not null)
        {
            appWindow.Closing += PreviewWindow_Closing;
        }
        autoHideTimer = DispatcherQueue.CreateTimer();
        autoHideTimer.Interval = AutoHideDelay;
        autoHideTimer.IsRepeating = false;
        autoHideTimer.Tick += AutoHideTimer_Tick;
    }

    private async Task RestorePlacementAsync(RectInt32 initialBounds)
    {
        if (appWindow is null)
        {
            return;
        }

        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var area = display.WorkArea;
        var savedPlacement = await placementStore.LoadLastAsync(CancellationToken.None);
        var placement = savedPlacement is null
            ? ClampInitialPlacement(initialBounds, DisplayKey(display), area)
            : PreviewWindowPlacementStore.Clamp(savedPlacement, DisplayKey(display), ToPreviewWorkArea(area));
        topmost = placement.Topmost;
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = topmost;
        }

        appWindow.MoveAndResize(new RectInt32(placement.X, placement.Y, placement.Width, placement.Height));
    }

    private static PreviewWindowPlacement ClampInitialPlacement(RectInt32 bounds, string displayId, RectInt32 area) =>
        PreviewWindowPlacementStore.Clamp(
            new PreviewWindowPlacement(displayId, bounds.X, bounds.Y, bounds.Width, bounds.Height, Topmost: true),
            displayId,
            ToPreviewWorkArea(area));

    private async Task EnsureShellAsync()
    {
        if (previewWebView.CoreWebView2 is not null)
        {
            return;
        }

        await previewWebView.EnsureCoreWebView2Async();
        var coreWebView = previewWebView.CoreWebView2
            ?? throw new InvalidOperationException("预览 WebView 未能初始化。");
        coreWebView.SetVirtualHostNameToFolderMapping(
            "app.huahai.local",
            assetsDirectory,
            CoreWebView2HostResourceAccessKind.Allow);
        coreWebView.WebMessageReceived += PreviewWebView_WebMessageReceived;
        previewWebView.Source = new Uri("https://app.huahai.local/Web/product-shell.html?surface=preview");
    }

    private async void PreviewWebView_WebMessageReceived(
        CoreWebView2 sender,
        CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (!WebBridgeRequest.TryParse(args.WebMessageAsJson, out var request) ||
            request is null ||
            !WebBridgeProtocol.IsSupported(request.Action))
        {
            return;
        }

        switch (request.Action)
        {
            case "previewReady":
                shellReady = true;
                await PostStateAsync();
                return;
            case "savePreview":
                await SaveAsync(request);
                return;
            case "previewCopy":
                await CopyAsync();
                return;
            case "discardPreview":
                dirty = false;
                if (string.Equals(request.Mode, "close", StringComparison.Ordinal))
                {
                    HidePreview();
                    return;
                }
                if (string.Equals(request.Mode, "switch", StringComparison.Ordinal) && pendingRecord is not null)
                {
                    var next = pendingRecord;
                    await OpenRecordAsync(next, pendingBounds);
                    return;
                }
                await PostStateAsync("已放弃未保存修改");
                TryStartAutoHide();
                return;
            case "previewDirty":
                dirty = request.Enabled == true;
                TryStartAutoHide();
                return;
            case "previewFocus":
                editorFocused = request.Enabled == true;
                TryStartAutoHide();
                return;
            case "previewPointer":
                pointerInside = request.Enabled == true;
                if (pointerInside) autoHideTimer?.Stop(); else TryStartAutoHide();
                return;
            case "previewTopmost":
                SetTopmost(request.Enabled == true);
                await PostStateAsync();
                await SavePlacementAsync();
                return;
            case "previewAutoHide":
                autoHideEnabled = request.Enabled == true;
                await PostStateAsync(autoHideEnabled ? "已开启自动隐藏" : "已关闭自动隐藏");
                return;
            case "previewHide":
                HidePreview();
                return;
            case "previewClose":
                RequestClose();
                return;
            case "beginNativeDrag":
                BeginNativeDrag();
                return;
        }
    }

    private async Task SaveAsync(WebBridgeRequest request)
    {
        if (record is null || request.Id is null || !Guid.TryParse(request.Id, out var recordId) || recordId != record.Id)
        {
            await PostStatusAsync("要保存的历史记录已不存在");
            return;
        }

        var result = await saveAsync(record.Id, new PreviewEdit(record.Kind, request.Text ?? string.Empty));
        if (!result.Succeeded || result.Record is null)
        {
            await PostStatusAsync(result.ErrorMessage ?? "保存失败，草稿仍可复制");
            return;
        }

        record = result.Record;
        dirty = false;
        saved(record);
        await PostStateAsync(result.ConvertedLinkToText ? "已保存为文本" : "已保存");
        if (string.Equals(request.Mode, "close", StringComparison.Ordinal))
        {
            HidePreview();
            return;
        }
        if (string.Equals(request.Mode, "switch", StringComparison.Ordinal) && pendingRecord is not null)
        {
            var next = pendingRecord;
            await OpenRecordAsync(next, pendingBounds);
            return;
        }
        TryStartAutoHide();
    }

    private async Task CopyAsync()
    {
        if (record is null)
        {
            return;
        }

        try
        {
            await copyAsync(record.Id);
            await PostStatusAsync("已复制");
        }
        catch (Exception exception)
        {
            await PostStatusAsync(exception.Message);
        }
    }

    private async Task PostStateAsync(string? status = null)
    {
        if (!shellReady || previewWebView.CoreWebView2 is null || record is null)
        {
            return;
        }

        var style = styleProvider();
        var paths = record.Kind == ClipboardItemKind.File
            ? record.PrimaryText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();
        var thumbnail = record.Kind == ClipboardItemKind.Image
            ? await thumbnailAsync(record, CancellationToken.None)
            : null;
        previewWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "previewState",
            themeId = style.ThemeId,
            opacity = style.Opacity,
            topmost,
            autoHide = autoHideEnabled,
            status = status ?? (record.Kind is ClipboardItemKind.File or ClipboardItemKind.Image ? "仅保存显示名称" : "可编辑完整内容"),
            record = new
            {
                id = record.Id.ToString("D"),
                kind = KindName(record.Kind),
                text = record.PrimaryText,
                displayName = record.DisplayName ?? DisplayName(record),
                paths,
                thumbnail
            }
        }));
    }

    private Task PostStatusAsync(string message)
    {
        if (!shellReady || previewWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        previewWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "previewStatus", message }));
        return Task.CompletedTask;
    }

    private Task PostConfirmationAsync(string mode)
    {
        if (!shellReady || previewWebView.CoreWebView2 is null)
        {
            return Task.CompletedTask;
        }

        previewWebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "previewConfirm", mode }));
        return Task.CompletedTask;
    }

    private void SetTopmost(bool enabled)
    {
        topmost = enabled;
        if (appWindow?.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsAlwaysOnTop = enabled;
        }
    }

    private void TryStartAutoHide()
    {
        if (autoHideEnabled && !pointerInside && !dirty && !editorFocused)
        {
            autoHideTimer?.Start();
        }
        else
        {
            autoHideTimer?.Stop();
        }
    }

    private void AutoHideTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args) => HidePreview();

    private void RequestClose()
    {
        if (dirty)
        {
            _ = PostConfirmationAsync("close");
            return;
        }

        HidePreview();
    }

    private void PreviewWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        RequestClose();
    }

    private void HidePreview()
    {
        autoHideTimer?.Stop();
        _ = SavePlacementAsync();
        _ = ShowWindow(WinRT.Interop.WindowNative.GetWindowHandle(this), ShowWindowHide);
    }

    private async Task SavePlacementAsync()
    {
        if (appWindow is null)
        {
            return;
        }

        var display = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var size = appWindow.Size;
        var position = appWindow.Position;
        await placementStore.SaveAsync(new PreviewWindowPlacement(
            DisplayKey(display), position.X, position.Y, size.Width, size.Height, topmost));
    }

    private void BeginNativeDrag()
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _ = ReleaseCapture();
        _ = SendMessage(handle, WmNcLeftButtonDown, new IntPtr(HitTestCaption), IntPtr.Zero);
    }

    private static PreviewWorkArea ToPreviewWorkArea(RectInt32 area) => new(area.X, area.Y, area.Width, area.Height);

    private static string DisplayKey(DisplayArea display) => display.DisplayId.Value.ToString("X16");

    private static string DisplayName(ClipboardRecord value) => value.Kind == ClipboardItemKind.File
        ? Path.GetFileName(value.PrimaryText.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty)
        : value.PrimaryText;

    private static string KindName(ClipboardItemKind kind) => kind switch
    {
        ClipboardItemKind.Text => "文本",
        ClipboardItemKind.Link => "链接",
        ClipboardItemKind.Image => "图片",
        ClipboardItemKind.File => "文件",
        _ => "文本"
    };

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);
}
