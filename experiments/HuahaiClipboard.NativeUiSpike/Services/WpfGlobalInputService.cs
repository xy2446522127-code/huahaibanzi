using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.Core.Privacy;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;

namespace HuahaiClipboard.NativeUiSpike.Services;

public sealed class WpfGlobalInputService : IDisposable
{
    private const int HotkeyId = 0x4843;
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmHotkey = 0x0312;
    private const int WhMouseLowLevel = 14;
    private const uint WmRightButtonDown = 0x0204;
    private const uint WmRightButtonUp = 0x0205;
    private const uint WmMiddleButtonDown = 0x0207;
    private const uint WmMiddleButtonUp = 0x0208;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;

    private readonly IntPtr windowHandle;
    private readonly HwndSource source;
    private readonly Dispatcher dispatcher;
    private readonly InputSettingsSnapshot settingsSnapshot;
    private readonly ClipboardCaptureService captureService;
    private readonly Action<IntPtr, Point> summon;
    private readonly HwndSourceHook windowHook;
    private readonly MouseHookProcedure mouseHookProcedure;
    private readonly PointerDoubleClickDetector rightDoubleClickDetector = new(
        global::System.Windows.Forms.SystemInformation.DoubleClickTime,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Width,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Height);
    private IntPtr mouseHook;
    private bool clipboardListenerRegistered;
    private bool disposed;
    private bool hotkeyRegistered;
    private bool suppressRightButtonUp;
    private uint suppressCustomMouseUp;

    public WpfGlobalInputService(
        IntPtr windowHandle,
        Dispatcher dispatcher,
        InputSettingsSnapshot settingsSnapshot,
        ClipboardCaptureService captureService,
        Action<IntPtr, Point> summon)
    {
        this.windowHandle = windowHandle;
        this.dispatcher = dispatcher;
        this.settingsSnapshot = settingsSnapshot;
        this.captureService = captureService;
        this.summon = summon;
        source = HwndSource.FromHwnd(windowHandle) ?? throw new InvalidOperationException("WPF window source is unavailable.");
        windowHook = WindowProcedure;
        mouseHookProcedure = MouseHook;
        source.AddHook(windowHook);
        clipboardListenerRegistered = AddClipboardFormatListener(windowHandle);
        ApplyHotkeyRegistration(settingsSnapshot.Current);
        mouseHook = SetWindowsHookEx(WhMouseLowLevel, mouseHookProcedure, GetModuleHandle(null), 0);
    }

    public IReadOnlyList<string> InitializationWarnings
    {
        get
        {
            var warnings = new List<string>();
            if (!clipboardListenerRegistered) warnings.Add("剪贴板监听未能启动，请重新启动花海剪贴板。");
            if (mouseHook == IntPtr.Zero) warnings.Add("右键双击监听未能启动，请使用托盘或自定义键盘快捷键。");
            if (settingsSnapshot.Current.HotkeyEnabled &&
                ShortcutGestureParser.TryParse(settingsSnapshot.Current.CustomShortcut, out var gesture) &&
                gesture?.Kind == ShortcutGestureKind.Keyboard &&
                !hotkeyRegistered)
            {
                warnings.Add("自定义键盘快捷键已被其他程序占用。");
            }

            return warnings;
        }
    }

    public void UpdateInputSettings(InputSettings settings)
    {
        settingsSnapshot.Update(settings);
        ApplyHotkeyRegistration(settings);
    }

    public static IntPtr GetForegroundTarget() => GetForegroundWindow();

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmClipboardUpdate)
        {
            var sourceWindow = GetForegroundWindow();
            _ = dispatcher.BeginInvoke(() =>
                _ = UnmanagedCallbackGuard.InvokeAsync(() => captureService.CaptureAsync(sourceWindow)));
        }
        else if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            var target = GetForegroundWindow();
            _ = GetCursorPos(out var point);
            dispatcher.BeginInvoke(() => summon(target, new Point(point.X, point.Y)));
            handled = true;
        }

        return IntPtr.Zero;
    }

    private IntPtr MouseHook(int code, UIntPtr wParam, IntPtr lParam) =>
        UnmanagedCallbackGuard.Invoke(
            () => MouseHookCore(code, wParam, lParam),
            () => CallNextHookEx(mouseHook, code, wParam, lParam));

    private IntPtr MouseHookCore(int code, UIntPtr wParam, IntPtr lParam)
    {
        if (code < 0) return CallNextHookEx(mouseHook, code, wParam, lParam);
        var message = unchecked((uint)wParam.ToUInt64());
        var settings = settingsSnapshot.Current;
        if (message == WmRightButtonDown)
        {
            var target = GetForegroundWindow();
            var identity = ClipboardCaptureService.WindowIdentity.FromHandle(target);
            var filter = new ClipboardPrivacyFilter(settings.ExcludedApplications);
            var mouse = Marshal.PtrToStructure<MouseHookData>(lParam);
            if (settings.RightDoubleClickEnabled &&
                !filter.ShouldExclude(identity.ProcessName, identity.WindowTitle) &&
                rightDoubleClickDetector.RegisterDown(mouse.time, mouse.point.X, mouse.point.Y))
            {
                dispatcher.BeginInvoke(() => summon(target, new Point(mouse.point.X, mouse.point.Y)));
                suppressRightButtonUp = true;
                return new IntPtr(1);
            }
        }
        else if (message == WmRightButtonUp && suppressRightButtonUp)
        {
            suppressRightButtonUp = false;
            return new IntPtr(1);
        }
        else if (message is WmMiddleButtonDown or WmXButtonDown && TrySummonCustomMouse(message, lParam, settings))
        {
            suppressCustomMouseUp = message == WmMiddleButtonDown ? WmMiddleButtonUp : WmXButtonUp;
            return new IntPtr(1);
        }
        else if (message == suppressCustomMouseUp)
        {
            suppressCustomMouseUp = 0;
            return new IntPtr(1);
        }

        return CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private bool TrySummonCustomMouse(uint message, IntPtr lParam, InputSettings settings)
    {
        if (!settings.HotkeyEnabled ||
            !ShortcutGestureParser.TryParse(settings.CustomShortcut, out var gesture) ||
            gesture is null || gesture.Kind == ShortcutGestureKind.Keyboard) return false;

        var mouse = Marshal.PtrToStructure<MouseHookData>(lParam);
        var matches = gesture.Kind switch
        {
            ShortcutGestureKind.MiddleMouse => message == WmMiddleButtonDown,
            ShortcutGestureKind.XButton1 => message == WmXButtonDown && HighWord(mouse.mouseData) == 1,
            ShortcutGestureKind.XButton2 => message == WmXButtonDown && HighWord(mouse.mouseData) == 2,
            _ => false,
        };
        if (!matches) return false;

        var target = GetForegroundWindow();
        var identity = ClipboardCaptureService.WindowIdentity.FromHandle(target);
        if (new ClipboardPrivacyFilter(settings.ExcludedApplications).ShouldExclude(identity.ProcessName, identity.WindowTitle)) return false;
        dispatcher.BeginInvoke(() => summon(target, new Point(mouse.point.X, mouse.point.Y)));
        return true;
    }

    private void ApplyHotkeyRegistration(InputSettings settings)
    {
        if (hotkeyRegistered)
        {
            _ = UnregisterHotKey(windowHandle, HotkeyId);
            hotkeyRegistered = false;
        }

        if (!settings.HotkeyEnabled ||
            !ShortcutGestureParser.TryParse(settings.CustomShortcut, out var gesture) ||
            gesture is not { Kind: ShortcutGestureKind.Keyboard }) return;

        hotkeyRegistered = RegisterHotKey(windowHandle, HotkeyId, gesture.Modifiers, gesture.VirtualKey);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (mouseHook != IntPtr.Zero) _ = UnhookWindowsHookEx(mouseHook);
        if (hotkeyRegistered) _ = UnregisterHotKey(windowHandle, HotkeyId);
        if (clipboardListenerRegistered) _ = RemoveClipboardFormatListener(windowHandle);
        source.RemoveHook(windowHook);
    }

    private static uint HighWord(uint value) => (value >> 16) & 0xFFFF;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint point;
        public uint mouseData;
        public uint flags;
        public uint time;
        public UIntPtr extraInfo;
    }

    private delegate IntPtr MouseHookProcedure(int code, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, MouseHookProcedure callback, IntPtr module, uint threadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
}
