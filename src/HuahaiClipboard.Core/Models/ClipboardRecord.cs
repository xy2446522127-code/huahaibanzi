namespace HuahaiClipboard.Core.Models;

public enum ClipboardItemKind { Text, Link, Image, File }

public sealed record ClipboardRecord(
    Guid Id,
    ClipboardItemKind Kind,
    string PrimaryText,
    string SecondaryText,
    DateTimeOffset LastCopiedAt,
    bool IsFavorite,
    bool IsPinned,
    bool IsAvailable,
    string? PreviewAssetPath);
