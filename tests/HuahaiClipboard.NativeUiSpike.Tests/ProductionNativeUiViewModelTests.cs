using HuahaiClipboard.Core.Contracts;
using System.IO;
using HuahaiClipboard.Core.Models;
using HuahaiClipboard.Core.Services;
using HuahaiClipboard.Core.Settings;
using HuahaiClipboard.NativeUiSpike.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.NativeUiSpike.Tests;

[TestClass]
public sealed class ProductionNativeUiViewModelTests
{
    [TestMethod]
    public async Task ProductionModel_LoadsAndMutatesTheRealPersistentHistory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-native-production-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var history = new JsonClipboardHistorySource(
                Path.Combine(directory, "history.dat"),
                new PassthroughProtector());
            var settings = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
            await settings.SaveAsync(
                ShellSettings.Default with { Behavior = new BehaviorSettings(true, 30) },
                CancellationToken.None);
            var first = Record("first", ClipboardItemKind.Text);
            var second = Record("second", ClipboardItemKind.Link);
            await history.UpsertAsync(first, CancellationToken.None);
            await history.UpsertAsync(second, CancellationToken.None);
            var clipboard = new RecordingClipboardPlatform();
            var actions = new ClipboardPanelActionSink(history, clipboard);
            var model = NativeUiSpikeViewModel.CreateProduction(history, actions, settings);

            await model.LoadAsync();

            Assert.AreEqual(2, model.AllItems.Count);
            Assert.AreEqual("最近 30 天 · 2 条", model.HeaderSummary);
            Assert.IsTrue(await model.CopyAsync(first.Id));
            Assert.AreEqual(first.Id, clipboard.LastWritten?.Id);

            await model.TogglePinnedAsync(first.Id);
            Assert.IsTrue((await history.FindAsync(first.Id, CancellationToken.None))?.IsPinned);

            await model.DeleteAsync(second.Id);
            Assert.AreEqual(1, (await history.GetAllAsync(CancellationToken.None)).Count);
            Assert.AreEqual("最近 30 天 · 1 条", model.HeaderSummary);

            var clearedProtected = await model.ClearOrdinaryAsync();
            Assert.AreEqual(0, clearedProtected);
            Assert.AreEqual(1, model.AllItems.Count);

            var clearedAll = await model.ClearAllAsync();
            Assert.AreEqual(1, clearedAll);
            Assert.AreEqual(0, (await history.GetAllAsync(CancellationToken.None)).Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ClipboardRecord Record(string text, ClipboardItemKind kind) => new(
        Guid.NewGuid(),
        kind,
        text,
        "test-source",
        DateTimeOffset.Now,
        false,
        false,
        true,
        null);

    private sealed class PassthroughProtector : ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }

    private sealed class RecordingClipboardPlatform : IClipboardPlatform
    {
        public ClipboardRecord? LastWritten { get; private set; }

        public Task WriteAsync(ClipboardRecord record, CancellationToken cancellationToken)
        {
            LastWritten = record;
            return Task.CompletedTask;
        }

        public Task<bool> PasteAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
