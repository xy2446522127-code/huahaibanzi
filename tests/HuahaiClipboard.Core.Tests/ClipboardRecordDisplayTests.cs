using System.Text.Json;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Presentation;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ClipboardRecordDisplayTests
{
    [TestMethod]
    public void File_UsesFileNameForTitleAndPreservesTheRealPathAsDetail()
    {
        const string path = @"F:\Users\DXY\Desktop\玛丽苏天花板？不确定再看看！.txt";
        var display = ClipboardRecordDisplay.From(Record(ClipboardItemKind.File, path, "explorer.exe"));

        Assert.AreEqual("玛丽苏天花板？不确定再看看！.txt", display.Title);
        Assert.AreEqual(path, display.Detail);
        Assert.IsFalse(display.HasThumbnail);
    }

    [TestMethod]
    public void MultipleFiles_KeepEveryPastePathButShowACompactReadableSummary()
    {
        var payload = string.Join(Environment.NewLine, @"C:\资料\甲.txt", @"D:\项目\乙.docx");
        var display = ClipboardRecordDisplay.From(Record(ClipboardItemKind.File, payload, "explorer.exe"));

        Assert.AreEqual("甲.txt 等 2 个文件", display.Title);
        Assert.AreEqual(@"C:\资料\甲.txt 等 2 个路径", display.Detail);
    }

    [TestMethod]
    public void ImageFile_UsesItsOriginalNameAndPathAndExposesAThumbnail()
    {
        const string sourcePath = @"F:\图片\花海参考图.png";
        var record = Record(
            ClipboardItemKind.Image,
            "花海截图-20260808-033358.png",
            "explorer.exe · 1920 x 1080 · PNG",
            previewAssetPath: @"F:\HuahaiClipboard\Data\user\images\encrypted.png",
            sourcePath: sourcePath);

        var display = ClipboardRecordDisplay.From(record);

        Assert.AreEqual("花海参考图.png", display.Title);
        Assert.AreEqual(sourcePath, display.Detail);
        Assert.IsTrue(display.HasThumbnail);
    }

    [TestMethod]
    public void BitmapWithoutAFilePath_KeepsItsGeneratedNameAndCaptureMetadata()
    {
        var record = Record(
            ClipboardItemKind.Image,
            "花海截图-20260808-033358.png",
            "Weixin.exe · 1920 x 1080 · PNG",
            previewAssetPath: @"F:\HuahaiClipboard\Data\user\images\encrypted.png");

        var display = ClipboardRecordDisplay.From(record);

        Assert.AreEqual("花海截图-20260808-033358.png", display.Title);
        Assert.AreEqual("Weixin.exe · 1920 x 1080 · PNG", display.Detail);
        Assert.IsTrue(display.HasThumbnail);
    }

    [TestMethod]
    public void LegacyJsonWithoutSourcePath_RemainsReadable()
    {
        var original = Record(
            ClipboardItemKind.Image,
            "花海截图-20260808-033358.png",
            "Weixin.exe · PNG",
            previewAssetPath: @"F:\HuahaiClipboard\Data\user\images\encrypted.png");
        var json = JsonSerializer.Serialize(original).Replace(",\"SourcePath\":null", string.Empty, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<ClipboardRecord>(json);

        Assert.IsNotNull(restored);
        Assert.IsNull(restored.SourcePath);
        Assert.AreEqual(original.PrimaryText, restored.PrimaryText);
    }

    [TestMethod]
    public async Task Search_FindsAnImageByItsOriginalSourcePath()
    {
        var record = Record(
            ClipboardItemKind.Image,
            "花海截图-20260808-033358.png",
            "explorer.exe · PNG",
            previewAssetPath: @"F:\HuahaiClipboard\Data\user\images\encrypted.png",
            sourcePath: @"F:\设计资料\最终版\花海参考图.png");
        var viewModel = new PanelViewModel(
            new SingleRecordHistorySource(record),
            new NoopActionSink(),
            new NoopNavigator());
        await viewModel.LoadAsync();

        viewModel.SearchText = "最终版";

        Assert.AreEqual(1, viewModel.VisibleRecords.Count);
        Assert.AreEqual(record.Id, viewModel.VisibleRecords[0].Id);
    }

    private static ClipboardRecord Record(
        ClipboardItemKind kind,
        string primaryText,
        string secondaryText,
        string? previewAssetPath = null,
        string? sourcePath = null) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            kind,
            primaryText,
            secondaryText,
            DateTimeOffset.Parse("2026-08-08T03:33:58+08:00"),
            IsFavorite: false,
            IsPinned: false,
            IsAvailable: true,
            PreviewAssetPath: previewAssetPath,
            SourcePath: sourcePath);

    private sealed class SingleRecordHistorySource(ClipboardRecord record) : IClipboardHistorySource
    {
        public Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClipboardRecord>>([record]);
        public Task<ClipboardRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken) =>
            Task.FromResult(record.Id == recordId ? record : null);
        public Task UpsertAsync(ClipboardRecord value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid recordId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearUnprotectedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task PruneAsync(DateTimeOffset cutoff, bool preserveProtected, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoopActionSink : IPanelActionSink
    {
        public Task<PanelActionResult> CopyAsync(Guid recordId, CancellationToken cancellationToken) =>
            Task.FromResult(PanelActionResult.Success());
        public Task<PanelActionResult> PasteAsync(Guid recordId, CancellationToken cancellationToken) =>
            Task.FromResult(PanelActionResult.Success());
    }

    private sealed class NoopNavigator : IWindowNavigator
    {
        public void ShowCursorPanel() { }
        public void ShowEdgePanel() { }
        public void ShowSettings() { }
        public void HideTransientPanel() { }
    }
}
