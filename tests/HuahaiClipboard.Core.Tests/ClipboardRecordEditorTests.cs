using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ClipboardRecordEditorTests
{
    [TestMethod]
    public async Task FileRename_ChangesDisplayNameWithoutChangingPayloadOrProtectedState()
    {
        await using var fixture = await HistoryFixture.CreateAsync(ClipboardItemKind.File, @"F:\资料\发布计划.docx");

        var result = await fixture.Source.ApplyPreviewEditAsync(
            fixture.Record.Id,
            new PreviewEdit(ClipboardItemKind.File, "发布计划"),
            CancellationToken.None);

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        var updated = result.Record!;
        Assert.AreEqual(fixture.Record.PrimaryText, updated.PrimaryText);
        Assert.AreEqual("发布计划", updated.DisplayName);
        Assert.AreEqual(fixture.Record.Id, updated.Id);
        Assert.AreEqual(fixture.Record.LastCopiedAt, updated.LastCopiedAt);
        Assert.AreEqual(fixture.Record.IsPinned, updated.IsPinned);
        Assert.AreEqual(fixture.Record.IsFavorite, updated.IsFavorite);
        Assert.AreEqual(fixture.Record.PreviewAssetPath, updated.PreviewAssetPath);
        Assert.AreEqual(fixture.Record.SourcePath, updated.SourcePath);
    }

    [TestMethod]
    public void InvalidLinkEdit_ConvertsTheRecordToText()
    {
        var record = CreateRecord(ClipboardItemKind.Link, "https://example.com/old");

        var result = ClipboardRecordEditor.Apply(
            record,
            new PreviewEdit(ClipboardItemKind.Link, "这不是一个网址"));

        Assert.IsTrue(result.Succeeded, result.ErrorMessage);
        Assert.IsTrue(result.ConvertedLinkToText);
        Assert.AreEqual(ClipboardItemKind.Text, result.Record!.Kind);
        Assert.AreEqual("这不是一个网址", result.Record.PrimaryText);
        Assert.IsNull(result.Record.DisplayName);
    }

    [TestMethod]
    public void BlankEdit_IsRejectedWithoutChangingTheRecord()
    {
        var record = CreateRecord(ClipboardItemKind.Text, "原始文本");

        var result = ClipboardRecordEditor.Apply(
            record,
            new PreviewEdit(ClipboardItemKind.Text, "   "));

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Record);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [TestMethod]
    public async Task MissingRecord_ReturnsRecoveryStateWithoutCreatingANewRecord()
    {
        await using var fixture = await HistoryFixture.CreateAsync(ClipboardItemKind.Text, "existing");

        var result = await fixture.Source.ApplyPreviewEditAsync(
            Guid.NewGuid(),
            new PreviewEdit(ClipboardItemKind.Text, "draft"),
            CancellationToken.None);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Record);
        Assert.AreEqual(1, (await fixture.Source.GetAllAsync(CancellationToken.None)).Count);
    }

    private static ClipboardRecord CreateRecord(ClipboardItemKind kind, string primaryText) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            kind,
            primaryText,
            "notepad.exe",
            DateTimeOffset.Parse("2026-08-31T10:00:00+08:00"),
            IsFavorite: true,
            IsPinned: true,
            IsAvailable: true,
            PreviewAssetPath: @"F:\HuahaiClipboard\Data\images\protected.bin",
            SourcePath: @"F:\资料\发布计划.docx");

    private sealed class HistoryFixture : IAsyncDisposable
    {
        private readonly string directory;

        private HistoryFixture(string directory, JsonClipboardHistorySource source, ClipboardRecord record)
        {
            this.directory = directory;
            Source = source;
            Record = record;
        }

        public JsonClipboardHistorySource Source { get; }
        public ClipboardRecord Record { get; }

        public static async Task<HistoryFixture> CreateAsync(ClipboardItemKind kind, string primaryText)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"huahai-preview-edit-{Guid.NewGuid():N}");
            var record = CreateRecord(kind, primaryText);
            var source = new JsonClipboardHistorySource(
                Path.Combine(directory, "history.dat"),
                new PassthroughTextProtector());
            await source.UpsertAsync(record, CancellationToken.None);
            return new HistoryFixture(directory, source, record);
        }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class PassthroughTextProtector : ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }
}
