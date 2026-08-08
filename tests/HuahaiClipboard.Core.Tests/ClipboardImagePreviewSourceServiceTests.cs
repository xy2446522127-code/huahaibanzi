using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ClipboardImagePreviewSourceServiceTests
{
    private static readonly byte[] PngBytes = [137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4];

    [TestMethod]
    public async Task ImagePreview_ReturnsAnInMemoryPngDataUrl()
    {
        var store = new RecordingImageStore(PngBytes);
        var service = new ClipboardImagePreviewSourceService(store);

        var actual = await service.CreateDataUrlAsync(ImageRecord("encrypted.png"), CancellationToken.None);

        Assert.AreEqual($"data:image/png;base64,{Convert.ToBase64String(PngBytes)}", actual);
        Assert.AreEqual(1, store.ReadCalls);
        Assert.AreEqual("encrypted.png", store.LastReadPath);
    }

    [TestMethod]
    public async Task NonImageRecord_DoesNotReadTheImageStore()
    {
        var store = new RecordingImageStore(PngBytes);
        var service = new ClipboardImagePreviewSourceService(store);

        var actual = await service.CreateDataUrlAsync(
            ImageRecord("encrypted.png") with { Kind = ClipboardItemKind.File },
            CancellationToken.None);

        Assert.IsNull(actual);
        Assert.AreEqual(0, store.ReadCalls);
    }

    [TestMethod]
    public async Task MissingOrUnreadablePreview_ReturnsNullForTheUiFallback()
    {
        var missingService = new ClipboardImagePreviewSourceService(new RecordingImageStore(PngBytes));
        var failingService = new ClipboardImagePreviewSourceService(
            new RecordingImageStore(new IOException("missing")));

        Assert.IsNull(await missingService.CreateDataUrlAsync(ImageRecord(null), CancellationToken.None));
        Assert.IsNull(await failingService.CreateDataUrlAsync(ImageRecord("missing.png"), CancellationToken.None));
    }

    private static ClipboardRecord ImageRecord(string? previewAssetPath) =>
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ClipboardItemKind.Image,
            "花海截图.png",
            "Weixin.exe · PNG",
            DateTimeOffset.Parse("2026-08-08T03:33:58+08:00"),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: previewAssetPath);

    private sealed class RecordingImageStore : IClipboardImageStore
    {
        private readonly byte[]? bytes;
        private readonly Exception? exception;

        public RecordingImageStore(byte[] bytes) => this.bytes = bytes;
        public RecordingImageStore(Exception exception) => this.exception = exception;

        public int ReadCalls { get; private set; }
        public string? LastReadPath { get; private set; }

        public Task<string> SaveAsync(string fileName, byte[] pngBytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
        {
            ReadCalls++;
            LastReadPath = filePath;
            return exception is null
                ? Task.FromResult(bytes!)
                : Task.FromException<byte[]>(exception);
        }

        public Task ProtectLegacyFilesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
