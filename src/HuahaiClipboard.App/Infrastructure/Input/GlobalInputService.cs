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
    private const int PreviewHotkeyId = 0x4850;
    private const uint WmClipboardUpdate = 0x031D;
    private const uint WmHotkey = 0x0312;
    private const int WhMouseLowLevel = 14;
    private const int WhKeyboardLowLevel = 13;
    private const uint WmLeftButtonDown = 0x0201;
    private const uint WmLeftButtonUp = 0x0202;
    private const uint WmRightButtonDown = 0x0204;
    private const uint WmRightButtonUp = 0x0205;
    private const uint WmMiddleButtonDown = 0x0207;
    private const uint WmMiddleButtonUp = 0x0208;
    private const uint WmMouseWheel = 0x020A;
    private const uint WmXButtonDown = 0x020B;
    private const uint WmXButtonUp = 0x020C;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierControl = 0x0002;
    private const uint ModifierShift = 0x0004;
    private const uint ModifierWindows = 0x0008;
    private const int VirtualKeyShift = 0x10;
    private const int VirtualKeyControl = 0x11;
    private const int VirtualKeyAlt = 0x12;
    private const int VirtualKeyLeftWindows = 0x5B;
    private const int VirtualKeyRightWindows = 0x5C;
    private const string HotkeyConflictWarning = "自定义键盘快捷键已被其他程序占用，请更换后重试。";
    private const string HotkeyReleaseWarning = "旧快捷键未能释放，重启花海剪贴板后将恢复。";
    private readonly IntPtr windowHandle;
    private readonly DispatcherQueue dispatcherQueue;
    private readonly InputSettingsSnapshot settingsSnapshot;
    private readonly ClipboardCaptureService captureService;
    private readonly Action<IntPtr, PointInt32> summonAction;
    private readonly Action previewShortcutAction;
    private readonly SubclassProcedure subclassProcedure;
    private readonly MouseHookProcedure mouseHookProcedure;
    private readonly KeyboardHookProcedure keyboardHookProcedure;
    private readonly KeyDoubleTapDetector customDoubleTapDetector = new();
    private readonly KeyDoubleTapDetector previewDoubleTapDetector = new();
    private readonly HashSet<uint> pressedKeys = [];
    private readonly List<string> initializationWarnings = [];
    private readonly PointerDoubleClickDetector rightDoubleClickDetector = new(
        global::System.Windows.Forms.SystemInformation.DoubleClickTime,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Width,
        global::System.Windows.Forms.SystemInformation.DoubleClickSize.Height);
    private IntPtr mouseHook;
    private IntPtr keyboardHook;
    private bool disposed;
    private bool subclassInstalled;
    private bool clipboardListenerRegistered;
    private bool hotkeyRegistered;
    private bool previewHotkeyRegistered;
    private bool previewHotkeyLeased;
    private bool suppressRightButtonUp;
    private uint suppressCustomMouseUp;

    public GlobalInputService(
        IntPtr windowHandle,
        DispatcherQueue dispatcherQueue,
        InputSettingsSnapshot settingsSnapshot,
        ClipboardCaptureService captureService,
        Action<IntPtr, PointInt32> summonAction,
        Action previewShortcutAction)
    {
        this.windowHandle = windowHandle;
        this.dispatcherQueue = dispatcherQueue;
        this.settingsSnapshot = settingsSnapshot;
        this.captureService = captureService;
        this.summonAction = summonAction;
        this.previewShortcutAction = previewShortcutAction;
        subclassProcedure = WindowSubclassProcedure;
        mouseHookProcedure = MouseHook;
        keyboardHookProcedure = KeyboardHook;

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

            ApplyHotkeyRegistration(settingsSnapshot.Current);
        }

        mouseHook = SetWindowsHookEx(WhMouseLowLevel, mouseHookProcedure, GetModuleHandle(null), 0);
        if (mouseHook == IntPtr.Zero)
        {
            initializationWarnings.Add("右键双击监听未能启动，请使用 Ctrl+Shift+V 或托盘图标。");
        }

        keyboardHook = SetWindowsHookEx(WhKeyboardLowLevel, keyboardHookProcedure, GetModuleHandle(null), 0);
        if (keyboardHook == IntPtr.Zero)
        {
            initializationWarnings.Add("双击键盘快捷键监听未能启动，请使用组合键或托盘图标。");
        }
    }

    public IReadOnlyList<string> InitializationWarnings => initializationWarnings;

    public void UpdateInputSettings(InputSettings settings)
    {
        settingsSnapshot.Update(settings);
        ApplyHotkeyRegistration(settings);
        ApplyPreviewHotkeyRegistration(settings);
    }

    public void UpdatePreviewShortcutLease(bool leased)
    {
        previewHotkeyLeased = leased;
        ApplyPreviewHotkeyRegistration(settingsSnapshot.Current);
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
        else if (message == WmHotkey && unchecked((int)wParam.ToUInt64()) == PreviewHotkeyId)
        {
            _ = dispatcherQueue.TryEnqueue(() => previewShortcutAction());
        }

        return DefSubclassProc(hwnd, message, wParam, lParam);
    }

    private IntPtr MouseHook(int code, UIntPtr wParam, IntPtr lParam) =>
        UnmanagedCallbackGuard.Invoke(
            () => MouseHookCore(code, wParam, lParam),
            () => CallNextHookEx(mouseHook, code, wParam, lParam));

    private IntPtr KeyboardHook(int code, UIntPtr wParam, IntPtr lParam) =>
        UnmanagedCallbackGuard.Invoke(
            () => KeyboardHookCore(code, wParam, lParam),
            () => CallNextHookEx(keyboardHook, code, wParam, lParam));

    private IntPtr KeyboardHookCore(int code, UIntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        var message = unchecked((uint)wParam.ToUInt64());
        var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
        if (message is WmKeyUp or WmSysKeyUp)
        {
            pressedKeys.Remove(data.virtualKey);
            return CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        if (message is not (WmKeyDown or WmSysKeyDown) || !pressedKeys.Add(data.virtualKey))
        {
            return CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        var settings = settingsSnapshot.Current;
        if (!settings.HotkeyEnabled || !ModifiersMatch(0))
        {
            customDoubleTapDetector.Reset();
            previewDoubleTapDetector.Reset();
            return CallNextHookEx(keyboardHook, code, wParam, lParam);
        }

        var customIsDoubleTap = ShortcutGestureParser.TryParse(settings.CustomShortcut, out var customGesture) &&
            customGesture is { Kind: ShortcutGestureKind.KeyboardDoubleTap };
        var customMatched = customIsDoubleTap && customDoubleTapDetector.RegisterDown(data.virtualKey, data.time);
        if (!customIsDoubleTap)
        {
            customDoubleTapDetector.Reset();
        }
        if (customMatched && customGesture!.VirtualKey == data.virtualKey)
        {
            var target = GetForegroundWindow();
            _ = GetCursorPos(out var point);
            _ = dispatcherQueue.TryEnqueue(() => summonAction(target, new PointInt32(point.X, point.Y)));
        }

        ShortcutGesture? previewGesture = null;
        var previewIsDoubleTap = previewHotkeyLeased &&
            PreviewShortcutLeasePolicy.ShouldLease(true, true, false, settings.PreviewShortcut, settings.CustomShortcut) &&
            ShortcutGestureParser.TryParse(settings.PreviewShortcut, out previewGesture) &&
            previewGesture is { Kind: ShortcutGestureKind.KeyboardDoubleTap };
        var previewMatched = previewIsDoubleTap && previewDoubleTapDetector.RegisterDown(data.virtualKey, data.time);
        if (!previewIsDoubleTap)
        {
            previewDoubleTapDetector.Reset();
        }
        if (previewMatched && previewGesture!.VirtualKey == data.virtualKey)
        {
            _ = dispatcherQueue.TryEnqueue(() => previewShortcutAction());
        }

        return CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHookCore(int code, UIntPtr wParam, IntPtr lParam)
    {
        if (code < 0)
        {
            return CallNextHookEx(mouseHook, code, wParam, lParam);
        }

        var message = unchecked((uint)wParam.ToUInt64());
        if (message is WmLeftButtonDown or WmRightButtonDown or WmMiddleButtonDown or WmMouseWheel or WmXButtonDown &&
            TrySummonCustomMouse(message, lParam))
        {
            suppressCustomMouseUp = message switch
            {
                WmLeftButtonDown => WmLeftButtonUp,
                WmRightButtonDown => WmRightButtonUp,
                WmMiddleButtonDown => WmMiddleButtonUp,
                WmXButtonDown => WmXButtonUp,
                _ => 0
            };
            return new IntPtr(1);
        }
        if (message == suppressCustomMouseUp && suppressCustomMouseUp != 0)
        {
            suppressCustomMouseUp = 0;
            return new IntPtr(1);
        }
        if (message == WmRightButtonDown)
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
        else if (message == WmRightButtonUp && suppressRightButtonUp)
        {
            suppressRightButtonUp = false;
            return new IntPtr(1);
        }

        return CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    private bool TrySummonCustomMouse(uint message, IntPtr lParam)
    {
        var settings = settingsSnapshot.Current;
        if (!settings.HotkeyEnabled ||
            !ShortcutGestureParser.TryParse(settings.CustomShortcut, out var gesture) ||
            gesture is null ||
            gesture.Kind == ShortcutGestureKind.Keyboard)
        {
            return false;
        }

        var mouse = Marshal.PtrToStructure<MouseHookData>(lParam);
        var matches = gesture.Kind switch
        {
            ShortcutGestureKind.LeftMouse => message == WmLeftButtonDown,
            ShortcutGestureKind.RightMouse => message == WmRightButtonDown,
            ShortcutGestureKind.MiddleMouse => message == WmMiddleButtonDown,
            ShortcutGestureKind.XButton1 => message == WmXButtonDown && HighWord(mouse.mouseData) == 1,
            ShortcutGestureKind.XButton2 => message == WmXButtonDown && HighWord(mouse.mouseData) == 2,
            ShortcutGestureKind.WheelUp => message == WmMouseWheel && unchecked((short)HighWord(mouse.mouseData)) > 0,
            ShortcutGestureKind.WheelDown => message == WmMouseWheel && unchecked((short)HighWord(mouse.mouseData)) < 0,
            _ => false
        };
        if (!matches || !ModifiersMatch(gesture.Modifiers))
        {
            return false;
        }

        var target = GetForegroundWindow();
        var identity = ClipboardCaptureService.WindowIdentity.FromHandle(target);
        var filter = new ClipboardPrivacyFilter(settings.ExcludedApplications);
        return !filter.ShouldExclude(identity.ProcessName, identity.WindowTitle) &&
               dispatcherQueue.TryEnqueue(() =>
                   summonAction(target, new PointInt32(mouse.point.X, mouse.point.Y)));
    }

    private void ApplyHotkeyRegistration(InputSettings settings)
    {
        customDoubleTapDetector.Reset();
        if (!subclassInstalled)
        {
            return;
        }

        if (hotkeyRegistered)
        {
            if (!UnregisterHotKey(windowHandle, HotkeyId))
            {
                if (!initializationWarnings.Contains(HotkeyReleaseWarning))
                {
                    initializationWarnings.Add(HotkeyReleaseWarning);
                }

                return;
            }

            hotkeyRegistered = false;
            initializationWarnings.Remove(HotkeyReleaseWarning);
        }

        initializationWarnings.Remove(HotkeyConflictWarning);
        if (!settings.HotkeyEnabled ||
            !ShortcutGestureParser.TryParse(settings.CustomShortcut, out var gesture) ||
            gesture is not { Kind: ShortcutGestureKind.Keyboard })
        {
            return;
        }

        hotkeyRegistered = RegisterHotKey(
            windowHandle,
            HotkeyId,
            gesture.Modifiers,
            gesture.VirtualKey);
        if (!hotkeyRegistered)
        {
            initializationWarnings.Add(HotkeyConflictWarning);
        }
    }

    private void ApplyPreviewHotkeyRegistration(InputSettings settings)
    {
        previewDoubleTapDetector.Reset();
        if (!subclassInstalled)
        {
            return;
        }

        if (previewHotkeyRegistered)
        {
            _ = UnregisterHotKey(windowHandle, PreviewHotkeyId);
            previewHotkeyRegistered = false;
        }

        if (!previewHotkeyLeased ||
            !PreviewShortcutLeasePolicy.ShouldLease(true, true, false, settings.PreviewShortcut, settings.CustomShortcut) ||
            !ShortcutGestureParser.TryParse(settings.PreviewShortcut, out var gesture) ||
            gesture is not { Kind: ShortcutGestureKind.Keyboard })
        {
            return;
        }

        previewHotkeyRegistered = RegisterHotKey(windowHandle, PreviewHotkeyId, gesture.Modifiers, gesture.VirtualKey);
    }

    private static uint HighWord(uint value) => (value >> 16) & 0xFFFF;

    private static bool ModifiersMatch(uint required) =>
        IsModifierMatch(required, ModifierControl, VirtualKeyControl) &&
        IsModifierMatch(required, ModifierAlt, VirtualKeyAlt) &&
        IsModifierMatch(required, ModifierShift, VirtualKeyShift) &&
        ((required & ModifierWindows) != 0) ==
        (IsKeyDown(VirtualKeyLeftWindows) || IsKeyDown(VirtualKeyRightWindows));

    private static bool IsModifierMatch(uint required, uint flag, int virtualKey) =>
        ((required & flag) != 0) == IsKeyDown(virtualKey);

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

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
        if (keyboardHook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(keyboardHook);
            keyboardHook = IntPtr.Zero;
        }

        if (hotkeyRegistered)
        {
            _ = UnregisterHotKey(windowHandle, HotkeyId);
            hotkeyRegistered = false;
        }
        if (previewHotkeyRegistered)
        {
            _ = UnregisterHotKey(windowHandle, PreviewHotkeyId);
            previewHotkeyRegistered = false;
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

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint virtualKey;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr extraInfo;
    }

    private delegate IntPtr SubclassProcedure(IntPtr hwnd, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData);
    private delegate IntPtr MouseHookProcedure(int code, UIntPtr wParam, IntPtr lParam);
    private delegate IntPtr KeyboardHookProcedure(int code, UIntPtr wParam, IntPtr lParam);

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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, KeyboardHookProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
