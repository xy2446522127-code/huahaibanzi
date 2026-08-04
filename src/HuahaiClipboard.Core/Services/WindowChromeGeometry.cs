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
        uint dpi)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(cornerRadius);

        var effectiveDpi = dpi == 0 ? 96u : dpi;
        var scaledDiameter = (int)Math.Round(
            cornerRadius * 2d * effectiveDpi / 96d,
            MidpointRounding.AwayFromZero);
        var cornerDiameter = Math.Clamp(scaledDiameter, 1, Math.Min(width, height));
        return new WindowChromeGeometry(width, height, cornerDiameter);
    }
}
