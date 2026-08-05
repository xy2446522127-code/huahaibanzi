using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;

namespace HuahaiClipboard.NativeUiSpike.Models;

public sealed class SpikeClipboardItem(
    Guid id,
    string stableId,
    ClipboardItemKind kind,
    string title,
    string metadata,
    bool pinned = false,
    bool favorite = false) : ObservableObject
{
    private bool isFavorite = favorite;
    private bool isPinned = pinned;

    public Guid Id { get; } = id;

    public string StableId { get; } = stableId;

    public ClipboardItemKind Kind { get; } = kind;

    public string KindGlyph => Kind switch
    {
        ClipboardItemKind.Text => "\uE8D2",
        ClipboardItemKind.Link => "\uE71B",
        ClipboardItemKind.Image => "\uE8B9",
        ClipboardItemKind.File => "\uE8A5",
        _ => "\uE8D2",
    };

    public string Title { get; } = title;

    public string Metadata { get; } = metadata;

    public bool IsPinned
    {
        get => isPinned;
        set => SetProperty(ref isPinned, value);
    }

    public bool IsFavorite
    {
        get => isFavorite;
        set => SetProperty(ref isFavorite, value);
    }

    public static SpikeClipboardItem FromRecord(ClipboardRecord record) => new(
        record.Id,
        record.Id.ToString("N"),
        record.Kind,
        record.PrimaryText,
        record.SecondaryText,
        record.IsPinned,
        record.IsFavorite);
}
