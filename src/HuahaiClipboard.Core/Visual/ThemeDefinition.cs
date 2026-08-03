namespace HuahaiClipboard.Core.Visual;

public sealed record ThemeDefinition(
    string Id,
    string DisplayName,
    string Accent,
    string Reflection,
    string GlassTop,
    string GlassBottom,
    string ContentLens,
    string FocusBorder,
    string TextPrimary,
    string TextSecondary);
