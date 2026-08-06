using System.Net;
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
            "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0\"}"));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 1));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual(new Version(1, 2, 0), result.LatestVersion);
        Assert.AreEqual(
            "https://github.com/xy2446522127-code/huahaibanzi/releases/tag/v1.2.0",
            result.ReleaseUrl);
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
}
