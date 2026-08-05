namespace HuahaiClipboard.Core.Services;

public enum ShortcutGestureKind
{
    Keyboard,
    MiddleMouse,
    XButton1,
    XButton2
}

public sealed record ShortcutGesture(
    ShortcutGestureKind Kind,
    uint Modifiers,
    uint VirtualKey);

public static class ShortcutGestureParser
{
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint Windows = 0x0008;

    private static readonly IReadOnlyDictionary<string, uint> NamedKeys =
        new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            ["Space"] = 0x20,
            ["Tab"] = 0x09,
            ["Enter"] = 0x0D,
            ["Return"] = 0x0D,
            ["Esc"] = 0x1B,
            ["Escape"] = 0x1B,
            ["Left"] = 0x25,
            ["Up"] = 0x26,
            ["Right"] = 0x27,
            ["Down"] = 0x28,
            ["Home"] = 0x24,
            ["End"] = 0x23,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["Insert"] = 0x2D,
            ["Delete"] = 0x2E
        };

    public static bool TryParse(string? value, out ShortcutGesture? gesture)
    {
        gesture = null;
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        gesture = normalized switch
        {
            "鼠标中键" => new ShortcutGesture(ShortcutGestureKind.MiddleMouse, 0, 0),
            "鼠标侧键 1" => new ShortcutGesture(ShortcutGestureKind.XButton1, 0, 0),
            "鼠标侧键 2" => new ShortcutGesture(ShortcutGestureKind.XButton2, 0, 0),
            _ => null
        };
        if (gesture is not null)
        {
            return true;
        }

        var parts = normalized
            .Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            modifiers |= part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => Control,
                "ALT" => Alt,
                "SHIFT" => Shift,
                "WIN" or "WINDOWS" => Windows,
                _ => 0
            };
        }

        var keyName = parts[^1];
        var key = ParseVirtualKey(keyName);
        if (key == 0)
        {
            gesture = null;
            return false;
        }

        var isFunctionKey = key is >= 0x70 and <= 0x87;
        if (modifiers == 0 && !isFunctionKey)
        {
            gesture = null;
            return false;
        }

        gesture = new ShortcutGesture(ShortcutGestureKind.Keyboard, modifiers, key);
        return true;
    }

    private static uint ParseVirtualKey(string value)
    {
        if (value.Length == 1)
        {
            var character = char.ToUpperInvariant(value[0]);
            if (character is >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                return character;
            }
        }

        if (value.Length is 2 or 3 &&
            value[0] is 'F' or 'f' &&
            int.TryParse(value.AsSpan(1), out var function) &&
            function is >= 1 and <= 24)
        {
            return (uint)(0x6F + function);
        }

        return NamedKeys.TryGetValue(value, out var key) ? key : 0;
    }
}
