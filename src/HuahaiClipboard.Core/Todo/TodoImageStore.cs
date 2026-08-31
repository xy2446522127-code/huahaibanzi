namespace HuahaiClipboard.Core.Todo;

public sealed record TodoStoredImage(string Path, string ContentType);

public sealed class TodoImageStore
{
    private static readonly IReadOnlyDictionary<string, string> Extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = "png",
        ["image/jpeg"] = "jpg",
        ["image/gif"] = "gif",
        ["image/webp"] = "webp"
    };

    private readonly string imageDirectory;

    public TodoImageStore(string imageDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageDirectory);
        this.imageDirectory = Path.GetFullPath(imageDirectory);
    }

    public async Task<TodoStoredImage> SaveDataUrlAsync(string dataUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dataUrl) || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("笔记图片必须是有效的 data URL。");
        }

        var separator = dataUrl.IndexOf(",", StringComparison.Ordinal);
        if (separator < 0)
        {
            throw new InvalidDataException("笔记图片缺少内容。");
        }

        var header = dataUrl[5..separator];
        var contentType = header.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (!header.Contains(";base64", StringComparison.OrdinalIgnoreCase) || !Extensions.TryGetValue(contentType, out var extension))
        {
            throw new InvalidDataException("仅支持 PNG、JPEG、GIF 或 WebP 图片。");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(dataUrl[(separator + 1)..]);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("笔记图片编码无效。", exception);
        }

        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024)
        {
            throw new InvalidDataException("笔记图片必须大于 0 且不超过 10 MB。");
        }

        Directory.CreateDirectory(imageDirectory);
        var path = Path.Combine(imageDirectory, $"{Guid.NewGuid():N}.{extension}");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        return new TodoStoredImage(path, contentType);
    }

    public async Task<string?> ReadDataUrlAsync(string imageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageId) || imageId != Path.GetFileName(imageId))
        {
            return null;
        }

        var path = Path.Combine(imageDirectory, imageId);
        if (!File.Exists(path))
        {
            return null;
        }

        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var contentType = Extensions.FirstOrDefault(pair => pair.Value == extension).Key;
        if (contentType is null)
        {
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }
}
