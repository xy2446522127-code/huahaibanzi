namespace HuahaiClipboard.Core.Settings;

public sealed record AppearanceSettings(
    string ThemeId,
    double Opacity,
    double BlurAmount,
    double ReflectionStrength,
    bool CompactMode,
    double PanelScale = 1d);
