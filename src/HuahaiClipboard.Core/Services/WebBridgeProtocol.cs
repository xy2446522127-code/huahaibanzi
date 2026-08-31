namespace HuahaiClipboard.Core.Services;

public static class WebBridgeProtocol
{
    private static readonly HashSet<string> SupportedActions = new(StringComparer.Ordinal)
    {
        "ready",
        "hide",
        "resize",
        "beginNativeDrag",
        "openPreview",
        "previewHover",
        "previewHoverEnd",
        "previewReady",
        "savePreview",
        "discardPreview",
        "previewCopy",
        "previewDirty",
        "previewFocus",
        "previewPointer",
        "previewTopmost",
        "previewAutoHide",
        "previewHide",
        "previewClose",
        "copy",
        "requestThumbnail",
        "togglePin",
        "toggleFavorite",
        "delete",
        "setRetentionDays",
        "setAutoCleanupCountEnabled",
        "setAutoCleanupCount",
        "clearOrdinary",
        "clearAll",
        "setTheme",
        "setOpacity",
        "setPetals",
        "setReduceMotion",
        "setClickDuration",
        "setRightDoubleClick",
        "setShortcut",
        "setPreviewShortcut",
        "resetShortcut",
        "setExclusions",
        "openDataFolder",
        "setStartup",
        "setBackground",
        "setOutsideAutoHide",
        "setPanelScale",
        "previewPanelScale",
        "commitPanelScale",
        "cancelPanelScale",
        "setCheckUpdatesOnStartup",
        "checkUpdate",
        "snoozeUpdate",
        "installUpdate",
        "openRelease"
        , "openTodoWorkspace", "todoReady", "todoAdd", "todoToggle", "todoDelete", "todoMove",
        "todoAddNote", "todoUpdateNote", "todoDeleteNote", "todoSetCapsule", "todoCollapse",
        "todoRestore", "todoClose", "todoTopmost"
    };

    public static bool IsSupported(string? action) =>
        action is not null && SupportedActions.Contains(action);
}
