using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HuahaiClipboard.Core.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseUrl,
    string InstallerName,
    string InstallerUrl,
    long InstallerSize,
    string InstallerSha256)
{
    public bool CanAutoInstall => UpdateAvailable && InstallerSize > 0
        && IsSha256(InstallerSha256);

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

public sealed class GitHubUpdateCheckService(HttpClient client, Version currentVersion)
{
    public const string ReleasesPage = "https://github.com/xy2446522127-code/huahaibanzi/releases";
    public const string InstallerAssetName = "HuahaiClipboard-Setup.exe";
    private const string LatestReleaseApi = "https://api.github.com/repos/xy2446522127-code/huahaibanzi/releases/latest";
    private const string LatestReleasePage = "https://github.com/xy2446522127-code/huahaibanzi/releases/latest";
    private static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim checkGate = new(1, 1);
    private DateTimeOffset lastCheckTime = DateTimeOffset.MinValue;
    private EntityTagHeaderValue? latestReleaseEtag;
    private UpdateCheckResult? lastSuccessfulResult;

    public static GitHubUpdateCheckService CreateDefault(Version currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HuahaiClipboard", currentVersion.ToString(3)));
        return new GitHubUpdateCheckService(client, currentVersion);
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        await checkGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sinceLastCheck = DateTimeOffset.Now - lastCheckTime;
            if (sinceLastCheck < MinimumCheckInterval && lastSuccessfulResult is not null)
            {
                return lastSuccessfulResult;
            }

            lastCheckTime = DateTimeOffset.Now;
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            if (latestReleaseEtag is not null)
            {
                request.Headers.IfNoneMatch.Add(latestReleaseEtag);
            }
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotModified && lastSuccessfulResult is not null)
            {
                return lastSuccessfulResult;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException("GitHub 仓库尚未发布可下载版本，请先创建 Release 后再检查。");
            }
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                lastSuccessfulResult = await CheckViaReleasePageAsync(cancellationToken).ConfigureAwait(false);
                return lastSuccessfulResult;
            }
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tag = document.RootElement.GetProperty("tag_name").GetString()?.Trim().TrimStart('v', 'V');
            var url = document.RootElement.GetProperty("html_url").GetString();
            var installer = FindInstallerAsset(document.RootElement);
            if (!Version.TryParse(tag, out var latest) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var releaseUri) ||
                releaseUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidDataException("GitHub Release 返回了无法识别的版本信息。");
            }

            var result = new UpdateCheckResult(
                latest > currentVersion,
                currentVersion,
                latest,
                releaseUri.AbsoluteUri,
                InstallerAssetName,
                installer.Url,
                installer.Size,
                installer.Sha256);
            latestReleaseEtag = response.Headers.ETag;
            lastSuccessfulResult = result;
            return result;
        }
        finally
        {
            checkGate.Release();
        }
    }

    private async Task<UpdateCheckResult> CheckViaReleasePageAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(LatestReleasePage, cancellationToken);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadAsStringAsync(cancellationToken);
        var tag = ExtractLatestTag(response.RequestMessage?.RequestUri?.AbsoluteUri, page)
            ?? throw new InvalidDataException("GitHub Release 网页暂时无法识别版本信息。");
        var cleanTag = tag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(cleanTag, out var latest))
        {
            throw new InvalidDataException("GitHub Release 网页暂时无法识别版本信息。");
        }

        var tagUrl = new Uri($"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/{tag.Trim()}");
        var downloadUrl = $"https://github.com/xy2446522127-code/huahaibanzi/releases/download/{tag.Trim()}/" + InstallerAssetName;
        var installer = await ProbeInstallerAsync(downloadUrl, cancellationToken);
        if (installer.Size <= 0)
        {
            return new UpdateCheckResult(
                latest > currentVersion,
                currentVersion,
                latest,
                tagUrl.AbsoluteUri,
                InstallerAssetName,
                string.Empty,
                0,
                string.Empty);
        }

        return new UpdateCheckResult(
            latest > currentVersion,
            currentVersion,
            latest,
            tagUrl.AbsoluteUri,
            InstallerAssetName,
            installer.Url,
            installer.Size,
            installer.Sha256);
    }

    private async Task<(string Url, long Size, string Sha256)> ProbeInstallerAsync(
        string url,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return (url, 0, string.Empty);
        }

        try
        {
            using var head = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Head, uri),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!head.IsSuccessStatusCode ||
                head.Content.Headers.ContentLength is not long length ||
                length <= 0)
            {
                return (url, 0, string.Empty);
            }

            return (url, length, string.Empty);
        }
        catch
        {
            return (url, 0, string.Empty);
        }
    }

    private static (string Url, long Size, string Sha256) ExtractInstallerMetadata(string page, string tag)
    {
        var nameMatch = Regex.Match(
            page,
            @"HuahaiClipboard-Setup\.exe",
            RegexOptions.IgnoreCase);
        if (!nameMatch.Success)
        {
            return (string.Empty, 0, string.Empty);
        }

        var sizeMatch = Regex.Match(
            page,
            @"(\d[\d,]*(?:\.\d+)?)\s*(?:KB|MB|GB)",
            RegexOptions.IgnoreCase);
        if (!sizeMatch.Success ||
            !double.TryParse(sizeMatch.Groups[1].Value.Replace(",", ""), out var displaySize) ||
            displaySize <= 0)
        {
            return (string.Empty, 0, string.Empty);
        }

        var suffix = sizeMatch.Value[^2..].ToUpperInvariant();
        long size = suffix switch
        {
            "KB" => (long)(displaySize * 1024),
            "MB" => (long)(displaySize * 1024 * 1024),
            "GB" => (long)(displaySize * 1024 * 1024 * 1024),
            _ => 0
        };
        if (size <= 0)
        {
            return (string.Empty, 0, string.Empty);
        }

        var shaMatch = Regex.Match(
            page,
            @"[0-9a-fA-F]{64}",
            RegexOptions.IgnoreCase);
        var sha256 = shaMatch.Success ? shaMatch.Groups[0].Value.ToLowerInvariant() : string.Empty;
        var downloadUrl = $"https://github.com/xy2446522127-code/huahaibanzi/releases/download/{tag}/" + InstallerAssetName;
        return (downloadUrl, size, sha256); // sha256 preserved even when size probe overwrites it
    }

    private static string? ExtractLatestTag(string? finalUrl, string page)
    {
        if (!string.IsNullOrEmpty(finalUrl) &&
            Uri.TryCreate(finalUrl, UriKind.Absolute, out var uri) &&
            uri.AbsolutePath.Contains("/releases/tag/", StringComparison.OrdinalIgnoreCase))
        {
            var candidate = uri.AbsolutePath
                .Substring(uri.AbsolutePath.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase) + "/releases/tag/".Length)
                .TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return Uri.UnescapeDataString(candidate);
            }
        }

        var match = Regex.Match(page, @"releases/tag/([^""'<>\s]+)", RegexOptions.IgnoreCase);
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value.TrimEnd('/')) : null;
    }

    public async Task<string> DownloadInstallerAsync(
        UpdateCheckResult release,
        string destinationDirectory,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
        if (!release.UpdateAvailable)
        {
            throw new InvalidOperationException("当前版本不需要更新。");
        }

        ValidateInstallerMetadata(
            release.InstallerName,
            release.InstallerUrl,
            release.InstallerSize,
            release.InstallerSha256);
        Directory.CreateDirectory(destinationDirectory);
        var destination = Path.Combine(destinationDirectory, InstallerAssetName);
        var temporary = destination + ".download";
        progress?.Report(0);
        try
        {
            using var response = await client.GetAsync(
                release.InstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength &&
                contentLength != release.InstallerSize)
            {
                throw new InvalidDataException("更新包大小与 GitHub Release 不一致。");
            }

            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = new FileStream(
                temporary,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                while (true)
                {
                    var read = await input.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    total += read;
                    if (total > release.InstallerSize)
                    {
                        throw new InvalidDataException("更新包超过 GitHub Release 声明的大小。");
                    }
                    progress?.Report(Math.Min(99, (int)(total * 100 / release.InstallerSize)));
                }
                if (total != release.InstallerSize)
                {
                    throw new InvalidDataException("更新包下载不完整。");
                }
            }

            string actualSha256;
            await using (var downloaded = File.OpenRead(temporary))
            {
                actualSha256 = Convert.ToHexString(
                    await SHA256.HashDataAsync(downloaded, cancellationToken)).ToLowerInvariant();
            }
            if (!string.Equals(actualSha256, release.InstallerSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("更新包 SHA-256 校验失败，已拒绝安装。");
            }

            File.Move(temporary, destination, overwrite: true);
            progress?.Report(100);
            return destination;
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    private static (string Url, long Size, string Sha256) FindInstallerAsset(JsonElement release)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub Release 缺少安装包。");
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.TryGetProperty("name", out var name) &&
                string.Equals(name.GetString(), InstallerAssetName, StringComparison.Ordinal))
            {
                var installerUrl = asset.TryGetProperty("browser_download_url", out var download)
                    ? download.GetString()
                    : null;
                var installerSize = asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var value)
                    ? value
                    : 0;
                if (!Uri.TryCreate(installerUrl, UriKind.Absolute, out var installerUri) ||
                    installerUri.Scheme != Uri.UriSchemeHttps ||
                    !string.Equals(installerUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                    installerSize <= 0)
                {
                    continue;
                }

                var digest = asset.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()
                    : null;
                var sha256 = digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true
                    ? digest[7..]
                    : string.Empty;
                return (installerUrl!, installerSize, sha256.ToLowerInvariant());
            }
        }

        throw new InvalidDataException($"GitHub Release 缺少 {InstallerAssetName}。");
    }

    private static void ValidateInstallerMetadata(
        string? name,
        string? url,
        long size,
        string? sha256)
    {
        if (!string.Equals(name, InstallerAssetName, StringComparison.Ordinal) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var installerUri) ||
            installerUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(installerUri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            size <= 0 ||
            sha256 is not { Length: 64 } ||
            !sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("GitHub Release 安装包元数据无效。");
        }
    }
}
