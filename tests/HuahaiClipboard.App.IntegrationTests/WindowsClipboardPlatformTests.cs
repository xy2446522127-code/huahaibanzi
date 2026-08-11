using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.App.IntegrationTests;

[TestClass]
public sealed class WindowsClipboardPlatformTests
{
    [TestMethod]
    public async Task WriteAsync_TextAndFilePayloadsCarryThePrivateOriginMarker()
    {
        var guard = new RecordingGuard();
        var written = new List<DataObject>();
        var platform = new WindowsClipboardPlatform(
            new FixedImageStore([]),
            guard,
            dataObject => written.Add(dataObject));

        await platform.WriteAsync(Record(ClipboardItemKind.Text, "花海文本"), CancellationToken.None);
        await platform.WriteAsync(
            Record(ClipboardItemKind.File, "F:\\one.txt" + Environment.NewLine + "F:\\two.docx"),
            CancellationToken.None);

        Assert.AreEqual("花海文本", written[0].GetText());
        Assert.AreEqual(guard.MarkerValue, written[0].GetData(guard.MarkerFormat, autoConvert: false));
        CollectionAssert.AreEqual(
            new[] { "F:\\one.txt", "F:\\two.docx" },
            written[1].GetFileDropList().Cast<string>().ToArray());
        Assert.AreEqual(guard.MarkerValue, written[1].GetData(guard.MarkerFormat, autoConvert: false));
        Assert.AreEqual(2, guard.SuccessfulWrites);
    }

    [TestMethod]
    public async Task WriteAsync_ImagePayloadAndMarkerAreDeliveredToTheClipboardSink()
    {
        byte[] pngBytes;
        using (var source = new Bitmap(2, 2))
        {
            source.SetPixel(0, 0, Color.DeepPink);
            using var stream = new MemoryStream();
            source.Save(stream, ImageFormat.Png);
            pngBytes = stream.ToArray();
        }

        var guard = new RecordingGuard();
        Bitmap? clipboardCopy = null;
        string? marker = null;
        var platform = new WindowsClipboardPlatform(
            new FixedImageStore(pngBytes),
            guard,
            dataObject =>
            {
                marker = dataObject.GetData(guard.MarkerFormat, autoConvert: false) as string;
                using var image = dataObject.GetImage();
                clipboardCopy = image is null ? null : new Bitmap(image);
            });

        await platform.WriteAsync(
            Record(ClipboardItemKind.Image, "image.png", previewAssetPath: "stored.png"),
            CancellationToken.None);

        Assert.AreEqual(guard.MarkerValue, marker);
        Assert.IsNotNull(clipboardCopy);
        using (clipboardCopy)
        {
            Assert.AreEqual(2, clipboardCopy.Width);
            Assert.AreEqual(2, clipboardCopy.Height);
            Assert.AreEqual(Color.DeepPink.ToArgb(), clipboardCopy.GetPixel(0, 0).ToArgb());
        }
        Assert.AreEqual(1, guard.SuccessfulWrites);
    }

    [TestMethod]
    public async Task WriteAsync_WhenClipboardSinkFails_DoesNotRecordAnOwnedSequence()
    {
        var guard = new RecordingGuard();
        var platform = new WindowsClipboardPlatform(
            new FixedImageStore([]),
            guard,
            _ => throw new InvalidOperationException("clipboard unavailable"));

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            platform.WriteAsync(Record(ClipboardItemKind.Text, "text"), CancellationToken.None));

        Assert.AreEqual(0, guard.SuccessfulWrites);
    }

    private static ClipboardRecord Record(
        ClipboardItemKind kind,
        string primaryText,
        string? previewAssetPath = null) =>
        new(
            Guid.NewGuid(),
            kind,
            primaryText,
            "test",
            DateTimeOffset.UtcNow,
            false,
            false,
            true,
            previewAssetPath);

    private sealed class RecordingGuard : IClipboardWriteOriginGuard
    {
        public string MarkerFormat => "HuahaiClipboard.InternalOrigin.v1";
        public string MarkerValue => "integration-token";
        public int SuccessfulWrites { get; private set; }
        public bool IsCurrentWrite() => false;
        public void RecordSuccessfulWrite() => SuccessfulWrites++;
    }

    private sealed class FixedImageStore(byte[] bytes) : IClipboardImageStore
    {
        public Task<string> SaveAsync(string fileName, byte[] pngBytes, CancellationToken cancellationToken) =>
            throw new AssertFailedException("Write path must not save images.");
        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken) =>
            Task.FromResult(bytes);
        public Task ProtectLegacyFilesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
