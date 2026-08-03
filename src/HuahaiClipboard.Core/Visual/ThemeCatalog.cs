namespace HuahaiClipboard.Core.Visual;

public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeDefinition> All { get; } = Array.AsReadOnly(
        new[]
        {
            new ThemeDefinition(
                Id: "rose-purple",
                DisplayName: "玫瑰紫",
                Accent: "#FFE9A6D1",
                Reflection: "#FFBC7CAF",
                GlassTop: "#B34C344F",
                GlassBottom: "#FF4B2A54",
                ContentLens: "#B3200E25",
                FocusBorder: "#9EE0B1D9",
                TextPrimary: "#FFFFFFFF",
                TextSecondary: "#CCFFFFFF"),
            new ThemeDefinition(
                Id: "cobalt-blue",
                DisplayName: "钴蓝",
                Accent: "#FF77B5FF",
                Reflection: "#FF4277D4",
                GlassTop: "#B32B466B",
                GlassBottom: "#FF1B3155",
                ContentLens: "#B30D1829",
                FocusBorder: "#9EA9D2FF",
                TextPrimary: "#FFFFFFFF",
                TextSecondary: "#CCFFFFFF"),
            new ThemeDefinition(
                Id: "emerald-cyan",
                DisplayName: "翡翠青",
                Accent: "#FF65DEC8",
                Reflection: "#FF289B91",
                GlassTop: "#B3295552",
                GlassBottom: "#FF173D3B",
                ContentLens: "#B30B2323",
                FocusBorder: "#9E91EBDD",
                TextPrimary: "#FFFFFFFF",
                TextSecondary: "#CCFFFFFF"),
            new ThemeDefinition(
                Id: "amber-orange",
                DisplayName: "琥珀橙",
                Accent: "#FFFFC26D",
                Reflection: "#FFD57942",
                GlassTop: "#B365422B",
                GlassBottom: "#FF4D2D1B",
                ContentLens: "#B32B180E",
                FocusBorder: "#9EFFD49A",
                TextPrimary: "#FFFFFFFF",
                TextSecondary: "#CCFFFFFF"),
            new ThemeDefinition(
                Id: "aurora-cyan-purple",
                DisplayName: "极光青紫",
                Accent: "#FF7FE8E0",
                Reflection: "#FF9B7DE3",
                GlassTop: "#B33A4E68",
                GlassBottom: "#FF352D58",
                ContentLens: "#B3141729",
                FocusBorder: "#9EB9C9F2",
                TextPrimary: "#FFFFFFFF",
                TextSecondary: "#CCFFFFFF")
        });
}
