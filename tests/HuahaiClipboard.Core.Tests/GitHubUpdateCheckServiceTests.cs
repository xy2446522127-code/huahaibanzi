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
