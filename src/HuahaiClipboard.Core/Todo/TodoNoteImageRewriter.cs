using System.Text.RegularExpressions;

namespace HuahaiClipboard.Core.Todo;

public sealed class TodoNoteImageRewriter(TodoImageStore imageStore)
{
    private static readonly Regex DataImageSource = new(
        """<img(?<before>[^>]*?)\s+src\s*=\s*"(?<source>data:image/[^"]+)"(?<after>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly TodoImageStore imageStore = imageStore ?? throw new ArgumentNullException(nameof(imageStore));

    private static readonly Regex StoredImage = new(
        """<img(?<before>[^>]*?)\s+data-image-id\s*=\s*"(?<id>[a-z0-9]+\.(?:png|jpg|gif|webp))"(?<after>[^>]*)>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<string> PersistAsync(string html, CancellationToken cancellationToken = default)
    {
        var source = html ?? string.Empty;
        var matches = DataImageSource.Matches(source);
        if (matches.Count == 0)
        {
            return source;
        }

        var offset = 0;
        var rewritten = new System.Text.StringBuilder(source.Length);
        foreach (Match match in matches)
        {
            rewritten.Append(source, offset, match.Index - offset);
            var stored = await imageStore.SaveDataUrlAsync(match.Groups["source"].Value, cancellationToken);
            var imageId = Path.GetFileName(stored.Path);
            rewritten.Append($"<img{match.Groups["before"].Value} data-image-id=\"{imageId}\"{match.Groups["after"].Value}>");
            offset = match.Index + match.Length;
        }

        rewritten.Append(source, offset, source.Length - offset);
        return rewritten.ToString();
    }

    public async Task<string> HydrateAsync(string html, CancellationToken cancellationToken = default)
    {
        var source = html ?? string.Empty;
        var matches = StoredImage.Matches(source);
        if (matches.Count == 0) return source;

        var offset = 0;
        var hydrated = new System.Text.StringBuilder(source.Length);
        foreach (Match match in matches)
        {
            hydrated.Append(source, offset, match.Index - offset);
            var dataUrl = await imageStore.ReadDataUrlAsync(match.Groups["id"].Value, cancellationToken);
            hydrated.Append(dataUrl is null
                ? $"<img{match.Groups["before"].Value}{match.Groups["after"].Value} alt=\"图片文件不存在\">"
                : $"<img{match.Groups["before"].Value} src=\"{dataUrl}\"{match.Groups["after"].Value}>");
            offset = match.Index + match.Length;
        }
        hydrated.Append(source, offset, source.Length - offset);
        return hydrated.ToString();
    }
}
