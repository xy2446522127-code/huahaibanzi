using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class WindowPlacementStoreTests
{
    [TestMethod]
    public async Task SaveAsync_RemembersEachDisplayAndTheMostRecentlyDraggedPosition()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-placement-{Guid.NewGuid():N}");
        var file = Path.Combine(directory, "window-positions.json");
        try
        {
            var store = new JsonWindowPlacementStore(file);
            await store.SaveAsync(new WindowPlacement("display-a", 120, 80));
            await store.SaveAsync(new WindowPlacement("display-b", 900, 140));

            Assert.AreEqual(new WindowPlacement("display-a", 120, 80), await store.LoadAsync("display-a"));
            Assert.AreEqual(new WindowPlacement("display-b", 900, 140), await store.LoadLastAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
