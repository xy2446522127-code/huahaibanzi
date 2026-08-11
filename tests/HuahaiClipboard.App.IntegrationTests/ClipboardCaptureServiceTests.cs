using System.Windows.Forms;
using HuahaiClipboard.App.Infrastructure.Clipboard;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.App.IntegrationTests;

[TestClass]
public sealed class ClipboardCaptureServiceTests
{
    [TestMethod]
    public async Task OwnedClipboardUpdate_DoesNotCreateHistoryOrRaiseHistoryChanged()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-owned-copy-{Guid.NewGuid():N}");
        try
        {
            var history = new JsonClipboardHistorySource(
                Path.Combine(directory, "history.dat"),
                new PassthroughTextProtector());
            var settings = new JsonSettingsStore(Path.Combine(directory, "settings.json"));
            var guard = new OwnedWriteGuard();
            var service = new ClipboardCaptureService(history, settings, new RejectingImageStore(), guard);
            var changed = 0;
            service.HistoryChanged += (_, _) => changed++;

            await service.CaptureAsync(IntPtr.Zero);

            Assert.AreEqual(1, guard.Checks);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, (await history.GetAllAsync(CancellationToken.None)).Count);
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
    public async Task FlushAsync_WaitsForAnInFlightClipboardCapture()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-capture-flush-{Guid.NewGuid():N}");
        var guard = new BlockingOwnedWriteGuard();
        try
        {
            var history = new JsonClipboardHistorySource(
                Path.Combine(directory, "history.dat"),
                new PassthroughTextProtector());
            var service = new ClipboardCaptureService(
                history,
                new JsonSettingsStore(Path.Combine(directory, "settings.json")),
                new RejectingImageStore(),
                guard);

            var capture = Task.Run(() => service.CaptureAsync(IntPtr.Zero));
            await guard.Entered.Task;
            var flush = service.FlushAsync();

            Assert.IsFalse(flush.IsCompleted, "The show path must not pass an active capture.");
            guard.Release.Set();
            await Task.WhenAll(capture, flush);
        }
        finally
        {
            guard.Release.Set();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task FlushAsync_WaitsForEveryCaptureRegisteredBeforeTheFlushSnapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-capture-burst-{Guid.NewGuid():N}");
        var guard = new BurstOwnedWriteGuard();
        try
        {
            var service = new ClipboardCaptureService(
                new JsonClipboardHistorySource(
                    Path.Combine(directory, "history.dat"),
                    new PassthroughTextProtector()),
                new JsonSettingsStore(Path.Combine(directory, "settings.json")),
                new RejectingImageStore(),
                guard);

            var first = Task.Run(() => service.CaptureAsync(IntPtr.Zero));
            await guard.FirstEntered.Task;
            var second = service.CaptureAsync(IntPtr.Zero);
            var third = service.CaptureAsync(IntPtr.Zero);
            var flush = service.FlushAsync();

            Assert.IsFalse(flush.IsCompleted);
            guard.ReleaseFirst.Set();
            await Task.WhenAll(first, second, third, flush);
            Assert.AreEqual(3, guard.Checks);
        }
        finally
        {
            guard.ReleaseFirst.Set();
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class OwnedWriteGuard : IClipboardWriteOriginGuard
    {
        public int Checks { get; private set; }
        public string MarkerFormat => "HuahaiClipboard.InternalOrigin.v1";
        public string MarkerValue => "test-token";
        public void AttachMarker(DataObject dataObject) =>
            dataObject.SetData(MarkerFormat, autoConvert: false, MarkerValue);
        public void RecordSuccessfulWrite() => throw new AssertFailedException("Capture must not write.");
        public bool IsCurrentWrite()
        {
            Checks++;
            return true;
        }
    }

    private sealed class BlockingOwnedWriteGuard : IClipboardWriteOriginGuard
    {
        public TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new(initialState: false);
        public string MarkerFormat => "HuahaiClipboard.InternalOrigin.v1";
        public string MarkerValue => "blocking-token";
        public void AttachMarker(DataObject dataObject) =>
            dataObject.SetData(MarkerFormat, autoConvert: false, MarkerValue);
        public void RecordSuccessfulWrite() => throw new AssertFailedException("Capture must not write.");
        public bool IsCurrentWrite()
        {
            Entered.TrySetResult();
            Release.Wait();
            return true;
        }
    }

    private sealed class BurstOwnedWriteGuard : IClipboardWriteOriginGuard
    {
        public TaskCompletionSource FirstEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim ReleaseFirst { get; } = new(initialState: false);
        public int Checks;
        public string MarkerFormat => "HuahaiClipboard.InternalOrigin.v1";
        public string MarkerValue => "burst-token";
        public void RecordSuccessfulWrite() => throw new AssertFailedException("Capture must not write.");
        public bool IsCurrentWrite()
        {
            var call = Interlocked.Increment(ref Checks);
            if (call == 1)
            {
                FirstEntered.TrySetResult();
                ReleaseFirst.Wait();
            }

            return true;
        }
    }

    private sealed class RejectingImageStore : IClipboardImageStore
    {
        public Task<string> SaveAsync(string fileName, byte[] pngBytes, CancellationToken cancellationToken) =>
            throw new AssertFailedException("Owned clipboard data must be rejected before image decoding.");
        public Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken) =>
            throw new AssertFailedException("Capture must not read image storage.");
        public Task ProtectLegacyFilesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PassthroughTextProtector : ITextProtector
    {
        public string Protect(string value) => value;
        public string Unprotect(string value) => value;
    }
}
