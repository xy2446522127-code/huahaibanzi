using HuahaiClipboard.Core.Todo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TodoNoteImageRewriterTests
{
    [TestMethod]
    public async Task PersistAsync_ReplacesPastedDataImagesWithLocalImageReferences()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-rewriter-{Guid.NewGuid():N}");
        try
        {
            var rewriter = new TodoNoteImageRewriter(new TodoImageStore(directory));

            var html = await rewriter.PersistAsync("<p>说明</p><img src=\"data:image/png;base64,AQID\">");

            StringAssert.Contains(html, "data-image-id=\"");
            Assert.IsFalse(html.Contains("data:image", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(1, Directory.GetFiles(directory).Length);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task HydrateAsync_RestoresStoredImageReferencesAsDataUrls()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-rewriter-{Guid.NewGuid():N}");
        try
        {
            var rewriter = new TodoNoteImageRewriter(new TodoImageStore(directory));
            var persisted = await rewriter.PersistAsync("<img src=\"data:image/png;base64,AQID\">");

            var hydrated = await rewriter.HydrateAsync(persisted);

            StringAssert.Contains(hydrated, "src=\"data:image/png;base64,AQID\"");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
