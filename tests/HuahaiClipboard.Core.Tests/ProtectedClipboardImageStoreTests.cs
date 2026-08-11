using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ProtectedClipboardImageStoreTests
{
    private static readonly byte[] PngBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];

    [TestMethod]
    public async Task SaveAsync_ProtectsBytesAtRestAndRoundTripsTheImage()
    {
        var directory = CreateDirectory();
        try
        {
            var store = new ProtectedClipboardImageStore(directory, new XorBinaryProtector());

            var path = await store.SaveAsync("花海截图-20260805-100000.png", PngBytes, CancellationToken.None);

            CollectionAssert.AreNotEqual(PngBytes, await File.ReadAllBytesAsync(path));
            CollectionAssert.AreEqual(PngBytes, await store.ReadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProtectLegacyFilesAsync_ReplacesPlainPngBytesWithoutBreakingReads()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "legacy.png");
            await File.WriteAllBytesAsync(path, PngBytes);
            var store = new ProtectedClipboardImageStore(directory, new XorBinaryProtector());

            await store.ProtectLegacyFilesAsync(CancellationToken.None);

            CollectionAssert.AreNotEqual(PngBytes, await File.ReadAllBytesAsync(path));
            CollectionAssert.AreEqual(PngBytes, await store.ReadAsync(path, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task DeleteUnreferencedAsync_RemovesOnlyOrphansInsideTheImageDirectory()
    {
        var directory = CreateDirectory();
        var outside = Path.Combine(Path.GetDirectoryName(directory)!, $"outside-{Guid.NewGuid():N}.png");
        try
        {
            var store = new ProtectedClipboardImageStore(directory, new XorBinaryProtector());
            var referenced = await store.SaveAsync("referenced.png", PngBytes, CancellationToken.None);
            var orphan = await store.SaveAsync("orphan.png", PngBytes, CancellationToken.None);
            await File.WriteAllBytesAsync(outside, PngBytes);

            await store.DeleteUnreferencedAsync([referenced, outside], CancellationToken.None);

            Assert.IsTrue(File.Exists(referenced));
            Assert.IsFalse(File.Exists(orphan));
            Assert.IsTrue(File.Exists(outside));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class XorBinaryProtector : IBinaryProtector
    {
        public byte[] Protect(byte[] value) => value.Select(item => (byte)(item ^ 0xA5)).ToArray();
        public byte[] Unprotect(byte[] value) => Protect(value);
    }
}
