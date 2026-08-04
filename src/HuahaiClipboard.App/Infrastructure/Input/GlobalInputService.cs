using System.Runtime.InteropServices;
using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.Core.Privacy;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using Microsoft.UI.Dispatching;
using Windows.Graphics;

namespace HuahaiClipboard.App.Infrastructure.Input;

public sealed class GlobalInputService : IDisposable
{
    private const int HotkeyId = 0x4843;
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmHotkey = 0x0312;
    private const int WhMouseLowLevel = 14;
    private const uint WmRightButtonDown = 0x0204;
    private const uint WmRightButtonUp = 0x0205;
    private const string HotkeyConflictWarning = "Ctrl+Shift+V 已被其他程序占用，请使用右键双击或托盘图标。";
    private const string HotkeyReleaseWarning = "Ctrl+Shift+V 未能释放，重启花海剪贴板后将恢复。";
    private readonly IntPtr windowHandle;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly InputSettingsSnapshot settingsSnapshot;
    private readonly ClipboardCaptureService captureService;
    private readonly Action<IntPtr, PointInt32> summonAction;
    private readonly SubclassProcedure subclassProcedure;
    private readonly MouseHookProcedure mouseHookProcedure;
    private readonly List<string> initializationWarnings = [];
    private readonly PointerDoubleClickDetector rightDoubleClickDetector = new(
        global::System.Windows.Forms.SystemInformation.DoubleClickTime,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Width,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Height);
    private IntPtr mouseHook;
    private bool disposed;
    private bool subclassInstalled;
    private bool clipboardListenerRegistered;
    private bool hotkeyRegistered;
    private bool suppressRightButtonUp;

    public GlobalInputService(
        IntPtr windowHandle,
        DispatcherQueue dispatcherQueue,
        InputSettingsSnapshot settingsSnapshot,
        ClipboardCaptureService captureService,
        Action<IntPtr, PointInt32> summonAction)
    {
        this.windowHandle = windowHandle;
        this.dispatcherQueue = dispatcherQueue;
        this.settingsSnapshot = settingsSnapshot;
        this.captureService = captureService;
        this.summonAction = summonAction;
        subclassProcedure = WindowSubclassProcedure;
        mouseHookProcedure = MouseHook;

        subclassInstalled = SetWindowSubclass(windowHandle, subclassProcedure, UIntPtr.Zero, UIntPtr.Zero);
        if (!subclassInstalled)
        {
            initializationWarnings.Add("剪贴板与快捷键监听未能启动，请通过托盘打开面板。");
        }
        else
        {
            clipboardListenerRegistered = AddClipboardFormatListener(windowHandle);
            if (!clipboardListenerRegistered)
            {
                initializationWarnings.Add("剪贴板监听未能启动，本次复制内容可能不会进入历史。");
            }

            ApplyHotkeyRegistration(settingsSnapshot.Current.HotkeyEnabled);
        }

        mouseHook = SetWindowsHookEx(WhMouseLowLevel, mouseHookProcedure, GetModuleHandle(null), 0);
        if (mouseHook == IntPtr.Zero)
        {
            initializationWarnings.Add("右键双击监听未能启动，请使用 Ctrl+Shift+V 或托盘图标。");
        }
    }

    public IReadOnlyList<string> InitializationWarnings => initializationWarnings;

    public void UpdateInputSettings(InputSettings settings)
    {
        settingsSnapshot.Update(settings);
        ApplyHotkeyRegistration(settings.HotkeyEnabled);
    }

    private IntPtr WindowSubclassProcedure(
        IntPtr hwnd,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (message == WmClipboardUpdate)
        {
            var source = GetForegroundWindow();
            _ = dispatcherQueue.TryEnqueue(() =>
                _ = UnmanagedCallbackGuard.InvokeAsync(() => captureService.CaptureAsync(source)));
        }
        else if (message == WmHotkey && unchecked((int)wParam.ToUInt64()) == HotkeyId)
        {
            var target = GetForegroundWindow();
            _ = GetCursorPos(out var point);
            if (settingsSnapshot.Current.HotkeyEnabled)
            {
                _ = dispatcherQueue.TryEnqueue(() => summonAction(target, new PointInt32(point.X, point.Y)));
            }
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private IntPtr MouseHook(int code, UIntPtr wParam, IntPtr lParam) =>
        UnmanagedCallbackGuard.Invoke(
            () => MouseHookCore(code, wParam, lParam),
            () => CallNextHookEx(mouseHook, code, wParam, lParam));

    private IntPtr MouseHookCore(int code, UIntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && wParam.ToUInt64() == WmRightButtonDown)
        {
            var target = GetForegroundWindow();
            var identity = ClipboardCaptureService.WindowIdentity.FromHandle(target);
            var settings = settingsSnapshot.Current;
            var filter = new ClipboardPrivacyFilter(settings.ExcludedApplications);
            var mouse = Marshal.PtrToStructure<MouseHookData>(lParam);
            if (settings.RightDoubleClickEnabled &&
                !filter.ShouldExclude(identity.ProcessName, identity.WindowTitle) &&
                rightDoubleClickDetector.RegisterDown(mouse.time, mouse.point.X, mouse.point.Y))
            {
                if (dispatcherQueue.TryEnqueue(() => summonAction(target, new PointInt32(mouse.point.X, mouse.point.Y))))
                {
                    suppressRightButtonUp = true;
                    return new IntPtr(1);
                }
            }
        }
        else if (code >= 0 && wParam.ToUInt64() == WmRightButtonUp && suppressRightButtonUp)
        {
            suppressRightButtonUp = false;
            return new IntPtr(1);
        }

        return CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private void ApplyHotkeyRegistration(bool enabled)
    {
        if (!subclassInstalled)
        {
            return;
        }

        if (enabled && !hotkeyRegistered)
        {
            hotkeyRegistered = RegisterHotKey(windowHandle, HotkeyId, 0x0002 | 0x0004, 0x56);
            if (hotkeyRegistered)
            {
                initializationWarnings.Remove(HotkeyConflictWarning);
                initializationWarnings.Remove(HotkeyReleaseWarning);
            }
            else if (!initializationWarnings.Contains(HotkeyConflictWarning))
            {
                initializationWarnings.Add(HotkeyConflictWarning);
            }
        }
        else if (!enabled && hotkeyRegistered)
        {
            if (UnregisterHotKey(windowHandle, HotkeyId))
            {
                hotkeyRegistered = false;
                initializationWarnings.Remove(HotkeyReleaseWarning);
            }
            else if (!initializationWarnings.Contains(HotkeyReleaseWarning))
            {
                initializationWarnings.Add(HotkeyReleaseWarning);
            }

            initializationWarnings.Remove(HotkeyConflictWarning);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (mouseHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(mouseHook);
            mouseHook = IntPtr.Zero;
        }

        if (hotkeyRegistered)
        {
            _ = UnregisterHotKey(windowHandle, HotkeyId);
            hotkeyRegistered = false;
        }

        if (clipboardListenerRegistered)
        {
            _ = RemoveClipboardFormatListener(windowHandle);
            clipboardListenerRegistered = false;
        }

        if (subclassInstalled)
        {
            _ = RemoveWindowSubclass(windowHandle, subclassProcedure, UIntPtr.Zero);
            subclassInstalled = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint point;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr extraInfo;
    }

    private delegate IntPtr SubclassProcedure(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData);
    private delegate IntPtr MouseHookProcedure(int code, UIntPtr wParam, IntPtr lParam);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hwnd, SubclassProcedure procedure, UIntPtr id, UIntPtr referenceData);

    [DllImport("comctl32.dll")]
    private static extern bool RemoveWindowSubclass(IntPtr hwnd, SubclassProcedure procedure, UIntPtr id);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, MouseHookProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
