using System.Net.Http.Headers;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace HuahaiClipboard.Core.Services;

public sealed record UpdateCheckResult(
    bool UpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string ReleaseUrl,
    string InstallerName,
    string InstallerUrl,
    long InstallerSize,
    string InstallerSha256);

public sealed class GitHubUpdateCheckService(HttpClient client, Version currentVersion)
{
    public const string ReleasesPage = "https://github.com/xy2446522127-code/huahaibanzi/releases";
    public const string InstallerAssetName = "HuahaiClipboard-Setup.exe";
    private const string LatestReleaseApi = "https://api.github.com/repos/xy2446522127-code/huahaibanzi/releases/latest";

    public static GitHubUpdateCheckService CreateDefault(Version currentVersion)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("HuahaiClipboard", currentVersion.ToString(3)));
        return new GitHubUpdateCheckService(client, currentVersion);
    }

    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(LatestReleaseApi, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("GitHub 仓库尚未发布可下载版本，请先创建 Release 后再检查。");
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

        return new UpdateCheckResult(
            latest > currentVersion,
            currentVersion,
            latest,
            releaseUri.AbsoluteUri,
            InstallerAssetName,
            installer.Url,
            installer.Size,
            installer.Sha256);
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
                var digest = asset.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()
                    : null;
                var sha256 = digest?.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) == true
                    ? digest[7..]
                    : string.Empty;
                ValidateInstallerMetadata(InstallerAssetName, installerUrl, installerSize, sha256);
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
            sha256 is null ||
            sha256.Length != 64 ||
            !sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("GitHub Release 安装包元数据无效。");
        }
    }
}
