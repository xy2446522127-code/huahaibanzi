using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace HuahaiClipboard.NativeUiSpike.Services;

public sealed class WindowCompositionService(Window window)
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;
    private const int GwlStyle = -16;
    private const long WsCaption = 0x00C00000L;
    private const long WsMaximizeBox = 0x00010000L;
    private const long WsMinimizeBox = 0x00020000L;
    private const long WsSysMenu = 0x00080000L;
    private const long WsThickFrame = 0x00040000L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private static readonly IntPtr HwndTopmost = new(-1);

    private IntPtr handle;

    public string CompositionMode { get; private set; } = "uninitialized";

    public void Apply()
    {
        handle = new WindowInteropHelper(window).EnsureHandle();
        if (HwndSource.FromHwnd(handle) is HwndSource source && source.CompositionTarget is not null)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
        }

        var style = GetWindowLongPtr(handle, GwlStyle).ToInt64();
        style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu);
        SetWindowLongPtr(handle, GwlStyle, new IntPtr(style));

        var rounded = 2;
        _ = DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref rounded, sizeof(int));
        var darkMode = 1;
        _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref darkMode, sizeof(int));
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        _ = DwmExtendFrameIntoClientArea(handle, ref margins);
        var backdrop = 3;
        var backdropResult = DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref backdrop, sizeof(int));
        CompositionMode = backdropResult == 0
            ? "dwm-transient-liquid-glass"
            : TryApplyAcrylicFallback()
                ? "windows-acrylic-liquid-glass"
                : "translucent-lens-fallback";
        ApplyRoundedRegion();
    }

    public void ApplyRoundedRegion()
    {
        if (handle == IntPtr.Zero) return;
        if (!ShouldApplyCustomRoundedRegion(Environment.OSVersion.Version.Build)) return;

        var dpi = VisualTreeHelper.GetDpi(window);
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * dpi.DpiScaleY));
        var radius = Math.Max(1, (int)Math.Round(29 * dpi.DpiScaleX));
        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        if (SetWindowRgn(handle, region, redraw: true) == 0) DeleteObject(region);
    }

    public static bool UsesSystemRoundedCorners(int windowsBuild) => windowsBuild >= 22000;

    public static bool ShouldApplyCustomRoundedRegion(int windowsBuild) => windowsBuild >= 19041;

    public void SetTopmost(bool enabled)
    {
        if (handle == IntPtr.Zero) handle = new WindowInteropHelper(window).EnsureHandle();
        _ = SetWindowPos(
            handle,
            enabled ? HwndTopmost : HwndNoTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    public void BringToForeground()
    {
        if (handle == IntPtr.Zero) handle = new WindowInteropHelper(window).EnsureHandle();
        _ = SetForegroundWindow(handle);
    }

    public static Point GetCursorPosition()
    {
        return GetCursorPos(out var point) ? new Point(point.X, point.Y) : new Point(0, 0);
    }

    private bool TryApplyAcrylicFallback()
    {
        var accent = new AccentPolicy
        {
            AccentState = 4,
            AccentFlags = 2,
            GradientColor = unchecked((int)0xB82D132D),
        };
        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPointer = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = 19,
                Data = accentPointer,
                SizeOfData = accentSize,
            };
            return SetWindowCompositionAttribute(handle, ref data) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr windowHandle, ref Margins margins);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr graphicObject);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr windowHandle, int index, IntPtr newValue);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr windowHandle, ref WindowCompositionAttributeData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
