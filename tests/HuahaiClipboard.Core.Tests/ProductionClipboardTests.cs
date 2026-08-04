using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Privacy;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text.Json;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class ProductionClipboardTests
{
    [TestMethod]
    public async Task History_UpsertDeduplicatesAndPersistsAcrossInstances()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "history.dat");
        try
        {
            var first = CreateRecord("first", DateTimeOffset.Parse("2026-08-04T10:00:00+08:00"));
            var duplicate = CreateRecord("first", DateTimeOffset.Parse("2026-08-04T10:01:00+08:00"));
            var source = new JsonClipboardHistorySource(path, new PassthroughTextProtector());

            await source.UpsertAsync(first, CancellationToken.None);
            await source.UpsertAsync(duplicate, CancellationToken.None);

            var reloaded = new JsonClipboardHistorySource(path, new PassthroughTextProtector());
            var records = await reloaded.GetAllAsync(CancellationToken.None);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual("first", records[0].PrimaryText);
            Assert.AreEqual(duplicate.LastCopiedAt, records[0].LastCopiedAt);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task History_LoadGivesLegacyImageRecordsAReadableFileName()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "history.dat");
        try
        {
            Directory.CreateDirectory(directory);
            ClipboardRecord[] legacy =
            [
                new(
                    Guid.NewGuid(),
                    ClipboardItemKind.Image,
                    "图片 2060 x 730",
                    "Weixin.exe · PNG",
                    DateTimeOffset.Parse("2026-08-04T16:42:05+08:00"),
                    false,
                    false,
                    true,
                    Path.Combine(directory, "legacy.png"))
            ];
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(legacy));

            var source = new JsonClipboardHistorySource(path, new PassthroughTextProtector());
            var records = await source.GetAllAsync(CancellationToken.None);

            Assert.AreEqual("花海截图-20260804-164205.png", records.Single().PrimaryText);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task History_CorruptFileIsQuarantinedBeforeNewHistoryIsWritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-corrupt-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "history.dat");
        const string corruptContents = "not-valid-history";
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, corruptContents);
            var source = new JsonClipboardHistorySource(path, new ThrowingTextProtector());

            await source.UpsertAsync(
                CreateRecord("new value", DateTimeOffset.Parse("2026-08-05T10:00:00+08:00")),
                CancellationToken.None);

            var quarantineFiles = Directory.GetFiles(directory, "history.dat.corrupt*");
            Assert.AreEqual(1, quarantineFiles.Length);
            Assert.AreEqual(corruptContents, await File.ReadAllTextAsync(quarantineFiles[0]));
            Assert.AreNotEqual(corruptContents, await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [DataTestMethod]
    [DataRow("Bitwarden.exe", "Bitwarden", true)]
    [DataRow("chrome.exe", "New Incognito Tab - Google Chrome", true)]
    [DataRow("msedge.exe", "InPrivate browsing", true)]
    [DataRow("notepad.exe", "Notes", false)]
    public void PrivacyFilter_BlocksPasswordManagersIncognitoAndUserExclusions(
        string processName,
        string windowTitle,
        bool expectedBlocked)
    {
        var filter = new ClipboardPrivacyFilter(["custom-secret.exe"]);

        Assert.AreEqual(expectedBlocked, filter.ShouldExclude(processName, windowTitle));
        Assert.IsTrue(filter.ShouldExclude("custom-secret.exe", "Document"));
    }

    [TestMethod]
    public async Task ActionSink_WritesTheSelectedRecordAndRejectsMissingRecords()
    {
        var record = CreateRecord("copied value", DateTimeOffset.UtcNow);
        var source = new InMemoryHistorySource(record);
        var platform = new RecordingClipboardPlatform();
        var sink = new ClipboardPanelActionSink(source, platform);

        var copied = await sink.CopyAsync(record.Id, CancellationToken.None);
        var missing = await sink.CopyAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.IsTrue(copied.Succeeded);
        Assert.AreEqual(record, platform.LastWrittenRecord);
        Assert.IsFalse(missing.Succeeded);
        Assert.AreEqual(1, platform.WriteCalls);
    }

    private static ClipboardRecord CreateRecord(string text, DateTimeOffset copiedAt) =>
        new(Guid.NewGuid(), ClipboardItemKind.Text, text, "notepad.exe", copiedAt, false, false, true, null);

    private sealed class PassthroughTextProtector : ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }

    private sealed class ThrowingTextProtector : ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => throw new FormatException("corrupt payload");
    }

    private sealed class RecordingClipboardPlatform : IClipboardPlatform
    {
        public int WriteCalls { get; private set; }
        public ClipboardRecord? LastWrittenRecord { get; private set; }

        public Task WriteAsync(ClipboardRecord record, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteCalls++;
            LastWrittenRecord = record;
            return Task.CompletedTask;
        }

        public Task<bool> PasteAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class InMemoryHistorySource(params ClipboardRecord[] records) : IClipboardHistorySource
    {
        private readonly List<ClipboardRecord> values = [.. records];

        public Task<IReadOnlyList<ClipboardRecord>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClipboardRecord>>(values.ToArray());

        public Task<ClipboardRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken) =>
            Task.FromResult(values.FirstOrDefault(record => record.Id == recordId));

        public Task UpsertAsync(ClipboardRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetFavoriteAsync(Guid recordId, bool value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetPinnedAsync(Guid recordId, bool value, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteAsync(Guid recordId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
