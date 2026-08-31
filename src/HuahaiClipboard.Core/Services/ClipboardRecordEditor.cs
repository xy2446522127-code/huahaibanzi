using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public static class ClipboardRecordEditor
{
    public static PreviewEditResult Apply(ClipboardRecord record, PreviewEdit edit)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(edit);

        if (record.Kind != edit.ExpectedKind)
        {
            return PreviewEditResult.ValidationError("记录已更新，请重新打开预览后再保存");
        }

        if (string.IsNullOrWhiteSpace(edit.Value))
        {
            return PreviewEditResult.ValidationError("内容不能为空");
        }

        return record.Kind switch
        {
            ClipboardItemKind.Text => new(record with
            {
                PrimaryText = edit.Value,
                DisplayName = null
            }, null, false),
            ClipboardItemKind.Link => ApplyLinkEdit(record, edit.Value),
            ClipboardItemKind.Image or ClipboardItemKind.File => new(record with
            {
                DisplayName = edit.Value.Trim()
            }, null, false),
            _ => PreviewEditResult.ValidationError("不支持编辑此记录")
        };
    }

    private static PreviewEditResult ApplyLinkEdit(ClipboardRecord record, string value)
    {
        var isHttpLink = Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
                         (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        return isHttpLink
            ? new(record with { PrimaryText = value, DisplayName = null }, null, false)
            : new(record with
            {
                Kind = ClipboardItemKind.Text,
                PrimaryText = value,
                DisplayName = null
            }, null, true);
    }
}
