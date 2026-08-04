using System.Globalization;

namespace HuahaiClipboard.Core.Services;

public static class ClipboardDisplayName
{
    public static string CreateImageFileName(DateTimeOffset copiedAt) =>
        $"花海截图-{copiedAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.png";
}
