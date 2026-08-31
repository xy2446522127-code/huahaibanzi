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
    public async Task UsesAValidatedStaticManifestWithoutCallingTheReleaseApi()
    {
        var handler = new StaticManifestHandler(
            """{"version":"1.1.13","releaseUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.13","installerUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.13/HuahaiClipboard-Setup.exe","size":42,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(
            client,
            new Version(1, 1, 12),
            new Uri("https://raw.githubusercontent.com/xy2446522127-code/huahaibanzi/master/update-manifest.json"));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.IsTrue(result.CanAutoInstall);
        Assert.AreEqual(new Version(1, 1, 13), result.LatestVersion);
        Assert.AreEqual(42L, result.InstallerSize);
        Assert.AreEqual(0, handler.ReleaseApiRequests);
    }

    [TestMethod]
    public async Task FallsBackToReleaseApiWhenStaticManifestIsMalformed()
    {
        var handler = new StaticManifestHandler("{\"version\":\"broken\"}");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(
            client,
            new Version(1, 1, 12),
            new Uri("https://raw.githubusercontent.com/xy2446522127-code/huahaibanzi/master/update-manifest.json"));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 1, 14), result.LatestVersion);
        Assert.AreEqual(1, handler.ReleaseApiRequests);
    }

    [TestMethod]
    public async Task RejectsAManifestUriOutsideTheFixedRepositoryLocation()
    {
        var handler = new StaticManifestHandler(
            """{"version":"1.1.13","releaseUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.13","installerUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.13/HuahaiClipboard-Setup.exe","size":42,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(
            client,
            new Version(1, 1, 12),
            new Uri("https://raw.githubusercontent.com/other-owner/other-repository/master/update-manifest.json"));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.AreEqual(new Version(1, 1, 14), result.LatestVersion);
        Assert.AreEqual(1, handler.ReleaseApiRequests);
    }

    [TestMethod]
    public async Task FallsBackToReleaseApiWhenStaticManifestVersionIsNotNewer()
    {
        var handler = new StaticManifestHandler(
            """{"version":"1.1.12","releaseUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.12","installerUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.12/HuahaiClipboard-Setup.exe","size":42,"sha256":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}""");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(
            client,
            new Version(1, 1, 12),
            GitHubUpdateCheckService.DefaultStaticManifestUri);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.AreEqual(new Version(1, 1, 14), result.LatestVersion);
        Assert.AreEqual(1, handler.ReleaseApiRequests);
    }

    [TestMethod]
    public async Task FallsBackToReleaseApiWhenStaticManifestMetadataIsInvalid()
    {
        var handler = new StaticManifestHandler(
            """{"version":"1.1.13","releaseUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.13","installerUrl":"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.13/HuahaiClipboard-Setup.exe","size":0,"sha256":"not-a-valid-sha256"}""");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(
            client,
            new Version(1, 1, 12),
            GitHubUpdateCheckService.DefaultStaticManifestUri);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.AreEqual(new Version(1, 1, 14), result.LatestVersion);
        Assert.AreEqual(1, handler.ReleaseApiRequests);
    }

    [TestMethod]
    public async Task AdjacentChecksReuseTheSuccessfulResultWithoutAThrottleFailure()
    {
        var handler = new CountingJsonHandler(
            "{\"tag_name\":\"v1.1.7\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.7\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.7/HuahaiClipboard-Setup.exe\",\"size\":123456}]}");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 6));

        var first = await service.CheckAsync(CancellationToken.None);
        var second = await service.CheckAsync(CancellationToken.None);

        Assert.AreEqual(first, second);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task OverlappingChecksShareOneHttpRequest()
    {
        var handler = new BlockingJsonHandler(
            "{\"tag_name\":\"v1.1.7\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.7\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.7/HuahaiClipboard-Setup.exe\",\"size\":123456}]}");
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 6));

        var first = service.CheckAsync(CancellationToken.None);
        await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = service.CheckAsync(CancellationToken.None);
        Assert.AreEqual(1, handler.RequestCount);

        handler.Release.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(results[0], results[1]);
        Assert.AreEqual(1, handler.RequestCount);
    }

    [TestMethod]
    public async Task ReusesTheLastSuccessfulReleaseWhenGitHubReturnsNotModified()
    {
        var handler = new EtagThenNotModifiedHandler();
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 6));

        var first = await service.CheckAsync(CancellationToken.None);
        typeof(GitHubUpdateCheckService)
            .GetField("lastCheckTime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(service, DateTimeOffset.MinValue);
        var second = await service.CheckAsync(CancellationToken.None);

        Assert.AreEqual(first, second);
        Assert.AreEqual(2, handler.RequestCount);
        Assert.AreEqual("\"release-v1.1.7\"", handler.SecondRequestEtag);
    }

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
        Assert.IsFalse(result.CanAutoInstall);
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
        using var client = new HttpClient(new RateLimitedThenPageHandler());
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.IsFalse(result.CanAutoInstall);
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
        Assert.IsFalse(result.CanAutoInstall);
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
    public async Task RejectsMissingDigestBeforeDownloadingInstaller()
    {
        var payload = Encoding.UTF8.GetBytes("unsigned fallback payload");
        var handler = new BytesHandler(payload);
        using var client = new HttpClient(handler);
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 10));
        var release = new UpdateCheckResult(
            true,
            new Version(1, 1, 10),
            new Version(1, 1, 11),
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.11",
            "HuahaiClipboard-Setup.exe",
            "https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.11/HuahaiClipboard-Setup.exe",
            payload.LongLength,
            string.Empty);
        var directory = Path.Combine(Path.GetTempPath(), $"HuahaiClipboard.UpdateTests.{Guid.NewGuid():N}");
        try
        {
            await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
                service.DownloadInstallerAsync(release, directory, null, CancellationToken.None));
            Assert.AreEqual(0, handler.RequestCount);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
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

    private sealed class RateLimitedThenPageHandler(string? tag = null) : HttpMessageHandler
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
            if (request.Method == HttpMethod.Head &&
                request.RequestUri.AbsoluteUri.EndsWith(
                    $"/releases/download/{tagValue}/HuahaiClipboard-Setup.exe",
                    StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                response.Content = new ByteArrayContent(Array.Empty<byte>());
                response.Content.Headers.ContentLength = 353370112L;
                return Task.FromResult(response);
            }

            if (request.Method == HttpMethod.Get &&
                request.RequestUri.AbsoluteUri.Contains("/releases/latest", StringComparison.OrdinalIgnoreCase))
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        $"<html><a href=\"/xy2446522127-code/huahaibanzi/releases/tag/{tagValue}\">Release</a></html>")
                };
                response.RequestMessage = new HttpRequestMessage(
                    HttpMethod.Get,
                    $"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/{tagValue}");
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
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

    private sealed class StaticManifestHandler(string manifest) : HttpMessageHandler
    {
        public int ReleaseApiRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host.Equals("raw.githubusercontent.com", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(manifest)
                });
            }

            if (request.RequestUri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                ReleaseApiRequests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"tag_name\":\"v1.1.14\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.14\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.14/HuahaiClipboard-Setup.exe\",\"size\":43,\"digest\":\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"}]}"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class CountingJsonHandler(string json) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            });
        }
    }

    private sealed class BlockingJsonHandler(string json) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };
        }
    }

    private sealed class EtagThenNotModifiedHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }
        public string? SecondRequestEtag { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 1)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"tag_name\":\"v1.1.7\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.1.7\",\"assets\":[{\"name\":\"HuahaiClipboard-Setup.exe\",\"browser_download_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/download/v1.1.7/HuahaiClipboard-Setup.exe\",\"size\":123456}]}"),
                };
                response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"release-v1.1.7\"");
                return Task.FromResult(response);
            }

            SecondRequestEtag = request.Headers.IfNoneMatch.SingleOrDefault()?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
        }
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
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            });
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
