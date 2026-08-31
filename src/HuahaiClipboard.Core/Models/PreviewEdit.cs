namespace HuahaiClipboard.Core.Models;

public sealed record PreviewEdit(ClipboardItemKind ExpectedKind, string Value);

public sealed record PreviewEditResult(
    ClipboardRecord? Record,
    string? ErrorMessage,
    bool ConvertedLinkToText)
{
    public bool Succeeded => Record is not null && string.IsNullOrWhiteSpace(ErrorMessage);

    public static PreviewEditResult ValidationError(string message) => new(null, message, false);

    public static PreviewEditResult RecordMissing() => new(null, "记录不存在或已删除", false);
}
