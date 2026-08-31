using HuahaiClipboard.Core.Todo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class TodoImageStoreTests
{
    [TestMethod]
    public async Task SaveDataUrlAsync_WritesImageOutsideWorkspaceJsonAndReturnsStableReference()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-todo-images-{Guid.NewGuid():N}");
        try
        {
            var store = new TodoImageStore(directory);

            var image = await store.SaveDataUrlAsync("data:image/png;base64,AQID");

            Assert.AreEqual("image/png", image.ContentType);
            StringAssert.StartsWith(image.Path, directory);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(image.Path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveDataUrlAsync_RejectsNonImagePayloads()
    {
        var store = new TodoImageStore(Path.Combine(Path.GetTempPath(), $"huahai-todo-images-{Guid.NewGuid():N}"));

        await Assert.ThrowsExceptionAsync<InvalidDataException>(() => store.SaveDataUrlAsync("data:text/plain;base64,SGVsbG8="));
    }
}
