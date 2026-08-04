namespace HuahaiClipboard.Core.Services;

public readonly record struct WindowChromeStyles(long Style, long ExtendedStyle);

public static class WindowChromeStyle
{
    private const long Caption = 0x00C00000;
    private const long ThickFrame = 0x00040000;
    private const long Border = 0x00800000;
    private const long DialogFrame = 0x00400000;
    private const long ExtendedWindowEdge = 0x00000100;
    private const long ExtendedClientEdge = 0x00000200;

    public static WindowChromeStyles WithoutNativeFrame(long style, long extendedStyle) =>
        new(
            style & ~(Caption | ThickFrame | Border | DialogFrame),
            extendedStyle & ~(ExtendedWindowEdge | ExtendedClientEdge));
}
