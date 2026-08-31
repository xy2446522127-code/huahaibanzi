namespace HuahaiClipboard.Core.Services;

public static class PreviewShortcutLeasePolicy
{
    public static bool ShouldLease(
        bool mainPanelVisible,
        bool hasHoveredRecord,
        bool settingsOpen,
        string? previewShortcut,
        string? summonShortcut)
    {
        if (!mainPanelVisible || !hasHoveredRecord || settingsOpen ||
            !ShortcutGestureParser.TryParse(previewShortcut, out var previewGesture) ||
            previewGesture is not { Kind: ShortcutGestureKind.Keyboard })
        {
            return false;
        }

        return !ShortcutGestureParser.TryParse(summonShortcut, out var summonGesture) ||
               summonGesture is null ||
               previewGesture != summonGesture;
    }
}
