namespace HuahaiClipboard.Core.Visual;

public static class ThemeCatalog
{
    public static IReadOnlyList<ThemeDefinition> All { get; } = Array.AsReadOnly(
        new[]
        {
            new ThemeDefinition(
                Id: "rose-purple",
                DisplayName: "玫瑰紫",
                Accent: "#FFD786BB",
                Reflection: "#FF8F5BAA",
                GlassTop: "#8FA05697",
                GlassBottom: "#E026112D",
                ContentLens: "#6B230E29",
                FocusBorder: "#A3D786BB",
                TextPrimary: "#FFFFF5FC",
                TextSecondary: "#FFC1B2C0"),
            new ThemeDefinition(
                Id: "cobalt-blue",
                DisplayName: "钴蓝",
                Accent: "#FF72AEF0",
                Reflection: "#FF365FA8",
                GlassTop: "#94436AA9",
                GlassBottom: "#E6111D3A",
                ContentLens: "#6B111D3A",
                FocusBorder: "#A372AEF0",
                TextPrimary: "#FFFFF5FC",
                TextSecondary: "#FFC1B2C0"),
            new ThemeDefinition(
                Id: "emerald-cyan",
                DisplayName: "翡翠青",
                Accent: "#FF6CCBAD",
                Reflection: "#FF287F77",
                GlassTop: "#94318474",
                GlassBottom: "#E60D312F",
                ContentLens: "#6B0D312F",
                FocusBorder: "#A36CCBAD",
                TextPrimary: "#FFFFF5FC",
                TextSecondary: "#FFC1B2C0"),
            new ThemeDefinition(
                Id: "amber-orange",
                DisplayName: "琥珀橙",
                Accent: "#FFE5AD70",
                Reflection: "#FFA36B56",
                GlassTop: "#949D654B",
                GlassBottom: "#E63A1F1C",
                ContentLens: "#6B3A1F1C",
                FocusBorder: "#A3E5AD70",
                TextPrimary: "#FFFFF5FC",
                TextSecondary: "#FFC1B2C0"),
            new ThemeDefinition(
                Id: "aurora-cyan-purple",
                DisplayName: "极光青紫",
                Accent: "#FF78D7DF",
                Reflection: "#FF8E72CF",
                GlassTop: "#94397B91",
                GlassBottom: "#E61A2246",
                ContentLens: "#6B1A2246",
                FocusBorder: "#A378D7DF",
                TextPrimary: "#FFFFF5FC",
                TextSecondary: "#FFC1B2C0")
        });
}
