using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public sealed record ClipboardRecordDisplay(
    string Title,
    string Detail,
    bool HasThumbnail)
{
    public static ClipboardRecordDisplay From(ClipboardRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.Kind switch
        {
            ClipboardItemKind.File => FromFiles(record),
            ClipboardItemKind.Image when !string.IsNullOrWhiteSpace(record.SourcePath) =>
                new(
                    SafeFileName(record.SourcePath, record.PrimaryText),
                    record.SourcePath,
                    HasImagePreview(record)),
            _ => new(
                record.PrimaryText,
                record.SecondaryText,
                HasImagePreview(record))
        };
    }

    private static ClipboardRecordDisplay FromFiles(ClipboardRecord record)
    {
        var paths = record.PrimaryText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paths.Length == 0)
        {
            return new(record.PrimaryText, record.SecondaryText, false);
        }

        var firstName = SafeFileName(paths[0], record.PrimaryText);
        return paths.Length == 1
            ? new(firstName, paths[0], false)
            : new(
                $"{firstName} 等 {paths.Length} 个文件",
                $"{paths[0]} 等 {paths.Length} 个路径",
                false);
    }

    private static bool HasImagePreview(ClipboardRecord record) =>
        record.Kind == ClipboardItemKind.Image &&
        !string.IsNullOrWhiteSpace(record.PreviewAssetPath);

    private static string SafeFileName(string path, string fallback)
    {
        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? fallback : fileName;
    }
}
