namespace HuahaiClipboard.Core.Services;

public static class GlassOpacityPolicy
{
    public static double Normalize(double value) => Math.Clamp(value, 0.65, 0.96);
}
