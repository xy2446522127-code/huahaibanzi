namespace HuahaiClipboard.Core.Services;

public readonly record struct WindowChromeGeometry(
    int Width,
    int Height,
    int CornerDiameter)
{
    public static WindowChromeGeometry Create(
        int width,
        int height,
        int cornerRadius,
        uint dpi) => Create(width, height, cornerRadius, dpi, 1d);

    public static WindowChromeGeometry Create(
        int width,
        int height,
        int cornerRadius,
        uint dpi,
        double panelScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(panelScale, 0d);

        var effectiveDpi = dpi == 0 ? 96u : dpi;
        var scaledDiameter = (int)Math.Round(
            cornerRadius * 2d * effectiveDpi / 96d * panelScale,
            MidpointRounding.AwayFromZero);
        var cornerDiameter = Math.Clamp(scaledDiameter, 1, Math.Min(width, height));
        return new WindowChromeGeometry(width, height, cornerDiameter);
    }

    public static WindowChromeGeometry CreateForWebView(
        int width,
        int height,
        int cornerRadius,
        uint dpi,
        double panelScale)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(panelScale, 0d);

        // The WebView shell cancels device-pixel-ratio with its root transform.
        // Applying DPI here would double-scale the CSS border radius and expose
        // opaque pixels at the native window corners.
        _ = dpi;
        var scaledDiameter = (int)Math.Round(
            cornerRadius * 2d * panelScale,
            MidpointRounding.AwayFromZero);
        var cornerDiameter = Math.Clamp(scaledDiameter, 1, Math.Min(width, height));
        return new WindowChromeGeometry(width, height, cornerDiameter);
    }
}
