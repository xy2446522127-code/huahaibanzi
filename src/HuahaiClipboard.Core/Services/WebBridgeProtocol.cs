namespace HuahaiClipboard.Core.Services;

public static class WebBridgeProtocol
{
    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    {
        "ready",
        "hide",
        "resize",
        "beginDrag",
        "dragMove",
        "endDrag",
        "copy",
        "togglePin",
        "toggleFavorite",
        "delete",
        "setRetentionDays",
        "clearOrdinary",
        "clearAll",
        "setTheme",
        "setOpacity",
        "setPetals",
        "setReduceMotion",
        "setClickDuration",
        "setRightDoubleClick",
        "setShortcut",
        "resetShortcut",
        "setExclusions",
        "openDataFolder",
        "setStartup",
        "setBackground",
        "setPanelScale",
        "setCheckUpdatesOnStartup",
        "checkUpdate",
        "installUpdate",
        "openRelease"
    };

    public static bool IsSupported(string? action) =>
        action is not null && SupportedActions.Contains(action);
}
