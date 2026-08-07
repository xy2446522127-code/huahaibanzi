using System.Net;
using System.Security.Cryptography;
using System.Text;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class GitHubUpdateCheckServiceTests
{
    [TestMethod]
    public async Task ReportsANewerReleaseWithoutDownloadingIt()
    {
        using var client = new HttpClient(new JsonHandler(
            "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe\",\"size\":345678,\"digest\":\"sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef\"}]}"));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 2, 0), result.LatestVersion);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0",
            result.ReleaseUrl);
        Assert.AreEqual("HuahaiClipboard-Setup.exe", result.InstallerName);
        Assert.AreEqual(345678L, result.InstallerSize);
        Assert.AreEqual(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            result.InstallerSha256);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe",
            result.InstallerUrl);
    }

    [TestMethod]
    public async Task DownloadsInstallerAndVerifiesReleaseDigest()
    {
        var payload = Encoding.UTF8.GetBytes("verified huahai installer payload");
        var digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        using var client = new HttpClient(new BytesHandler(payload));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));
        var release = new UpdateCheckResult(
            true,
            new Version(1, 1, 1),
            new Version(1, 2, 0),
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0",
            "HuahaiClipboard-Setup.exe",
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe",
            payload.LongLength,
            digest);
        var directory = Path.Combine(Path.GetTempPath(), $"HuahaiClipboard.UpdateTests.{Guid.NewGuid():N}");
        try
        {
            var progressValues = new List<int>();
            var path = await service.DownloadInstallerAsync(
                release,
                directory,
                new InlineProgress<int>(progressValues.Add),
                CancellationToken.None);

            CollectionAssert.AreEqual(payload, await File.ReadAllBytesAsync(path));
            Assert.AreEqual(100, progressValues[^1]);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task RejectsAndDeletesInstallerWhenDigestDoesNotMatch()
    {
        var payload = Encoding.UTF8.GetBytes("tampered installer");
        using var client = new HttpClient(new BytesHandler(payload));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));
        var release = new UpdateCheckResult(
            true,
            new Version(1, 1, 1),
            new Version(1, 2, 0),
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0",
            "HuahaiClipboard-Setup.exe",
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe",
            payload.LongLength,
            new string('0', 64));
        var directory = Path.Combine(Path.GetTempPath(), $"HuahaiClipboard.UpdateTests.{Guid.NewGuid():N}");
        try
        {
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
                service.DownloadInstallerAsync(release, directory, null, CancellationToken.None));
            Assert.IsFalse(File.Exists(Path.Combine(directory, "HuahaiClipboard-Setup.exe")));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public async Task ExplainsWhenTheRepositoryHasNoReleaseYet()
    {
        using var client = new HttpClient(new StatusHandler(HttpStatusCode.NotFound));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => service.CheckAsync(CancellationToken.None));

        StringAssert.Contains(exception.Message, "尚未发布可下载版本");
    }

    [TestMethod]
    public async Task FallsBackToReleasePageWhenApiIsRateLimited()
    {
        using var client = new HttpClient(new RateLimitedThenPageHandler());
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 2, 0), result.LatestVersion);
        Assert.IsTrue(result.CanAutoInstall);
        Assert.AreEqual(353370112L, result.InstallerSize);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe",
            result.InstallerUrl);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0",
            result.ReleaseUrl);
    }

    [TestMethod]
    public async Task ReleasePageFallbackProvidesSizeWithoutDigest()
    {
        using var client = new HttpClient(new RateLimitedThenPageHandler(includeDigest: false));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.IsTrue(result.CanAutoInstall);
        Assert.AreEqual(353370112L, result.InstallerSize);
        Assert.AreEqual(string.Empty, result.InstallerSha256);
    }

    [TestMethod]
    public async Task ApiWithoutDigestKeepsApiResult()
    {
        using var client = new HttpClient(new ApiOkNoDigestThenPageHandler());
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 2, 0), result.LatestVersion);
        Assert.IsTrue(result.CanAutoInstall);
        Assert.AreEqual(345678L, result.InstallerSize);
        Assert.AreEqual(string.Empty, result.InstallerSha256);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe",
            result.InstallerUrl);
    }

    private sealed class ApiOkNoDigestThenPageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(
                    "https://api.github.com/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.2.0/HuahaiClipboard-Setup.exe\",\"size\":345678}]}")
                });
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "<html><a href=\"/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0\">Release</a><div>HuahaiClipboard-Setup.exe size=345678 sha256=0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef</div></html>")
            };
            response.RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0");
            return Task.FromResult(response);
        }
    }

    [TestMethod]
    public async Task ReleasePageFallbackReportsCurrentWhenVersionIsNewer()
    {
        using var client = new HttpClient(new RateLimitedThenPageHandler("v1.1.1"));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 4));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsFalse(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 1, 1), result.LatestVersion);
    }

    private sealed class RateLimitedThenPageHandler(string? tag = null, bool includeDigest = true) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsoluteUri.StartsWith(
                    "https://api.github.com/", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            }

            var tagValue = tag ?? "v1.2.0";
            var notes = includeDigest
                ? "HuahaiClipboard-Setup.exe 337.0 MB"
                : "HuahaiClipboard-Setup.exe 337.0 MB";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $"<html><a href=\"/xy2446522127-code/huahaibanzi/releases/tag/{tagValue}\">Release</a><div>{notes}</div></html>")
            };
            response.RequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/{tagValue}");
            return Task.FromResult(response);
        }
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
    }

    private sealed class StatusHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class BytesHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
