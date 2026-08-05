using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HuahaiClipboard.NativeUiSpike.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseUrl);

public sealed class GitHubUpdateCheckService(HttpClient client, Version currentVersion)
{
    public const string ReleasesPage = "https://github.com/xy2446522127-code/huahaibanzi/releases";
    private const string LatestReleaseApi = "https://api.github.com/repos/xy2446522127-code/huahaibanzi/releases/latest";

    public static GitHubUpdateCheckService CreateDefault()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HuahaiClipboard", "1.1"));
        return new GitHubUpdateCheckService(
            client,
            typeof(GitHubUpdateCheckService).Assembly.GetName().Version ?? new Version(1, 1, 0));
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var tag = document.RootElement.GetProperty("tag_name").GetString()?.Trim().TrimStart('v', 'V');
        var url = document.RootElement.GetProperty("html_url").GetString();
        if (!Version.TryParse(tag, out var latest) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var releaseUri) ||
            releaseUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException("GitHub Release 返回了无法识别的版本信息。");
        }

        return new UpdateCheckResult(latest > currentVersion, currentVersion, latest, releaseUri.AbsoluteUri);
    }
}
