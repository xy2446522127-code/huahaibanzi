using System.Security.Cryptography;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;

namespace HuahaiClipboard.Core.Services;

public sealed class ClipboardImagePreviewSourceService(IClipboardImageStore imageStore)
{
    private const string PngDataUrlPrefix = "data:image/png;base64,";

    public async Task<string?> CreateDataUrlAsync(
        ClipboardRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Kind != ClipboardItemKind.Image ||
            string.IsNullOrWhiteSpace(record.PreviewAssetPath))
        {
            return null;
        }

        try
        {
            var bytes = await imageStore.ReadAsync(record.PreviewAssetPath, cancellationToken);
            return bytes.Length == 0
                ? null
                : PngDataUrlPrefix + Convert.ToBase64String(bytes);
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidDataException or
            CryptographicException)
        {
            return null;
        }
    }
}
