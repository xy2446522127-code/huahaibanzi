using System.Net;
using System.Net.Http;
using HuahaiClipboard.NativeUiSpike.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class GitHubUpdateCheckServiceTests
{
    [TestMethod]
    public async Task ReportsANewerGitHubReleaseWithoutDownloadingIt()
    {
        using var client = new HttpClient(new JsonHandler(
            "{\"tag_name\":\"v1.2.0\",\"html_url\":\"https://github.com/example/releases/tag/v1.2.0\"}"));
        var service = new GitHubUpdateCheckService(client, new Version(1, 1, 0));

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.IsTrue(result.UpdateAvailable);
        Assert.AreEqual("1.2.0", result.LatestVersion.ToString());
        Assert.AreEqual("https://github.com/example/releases/tag/v1.2.0", result.ReleaseUrl);
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json),
            });
    }
}
