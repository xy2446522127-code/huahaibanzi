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
