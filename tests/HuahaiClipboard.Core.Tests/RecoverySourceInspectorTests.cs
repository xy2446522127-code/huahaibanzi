using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoverySourceInspectorTests
{
    [TestMethod]
    public async Task InspectAsync_ReportsMalformedHistoryWithoutRenamingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-inspection-{Guid.NewGuid():N}");
        var historyPath = Path.Combine(root, "history.dat");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(historyPath, "not-a-dpapi-payload");
            var source = new RecoverySource(root, RecoverySourceKind.CorruptHistory, "fixture");

            var result = await new RecoverySourceInspector(new ThrowingRecoveryDataReader(new FormatException("bad data")))
                .InspectAsync(source, CancellationToken.None);

            Assert.AreEqual(RecoveryInspectionState.Malformed, result.State);
            Assert.IsTrue(File.Exists(historyPath));
            Assert.IsFalse(File.Exists(historyPath + ".corrupt"));
            Assert.AreEqual(1, result.FileManifest.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task InspectAsync_RecordsReadableCountsAndHashesFromTheActualDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-inspection-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "images"));
            await File.WriteAllTextAsync(Path.Combine(root, "history.dat"), "fixture");
            await File.WriteAllTextAsync(Path.Combine(root, "images", "image.png"), "image");
            var source = new RecoverySource(root, RecoverySourceKind.InstallDataRoot, "fixture");

            var result = await new RecoverySourceInspector(new FixedRecoveryDataReader(7, 2, 3, 1))
                .InspectAsync(source, CancellationToken.None);

            Assert.AreEqual(RecoveryInspectionState.Readable, result.State);
            Assert.AreEqual(7, result.HistoryCount);
            Assert.AreEqual(2, result.TodoCount);
            Assert.AreEqual(3, result.NoteCount);
            Assert.AreEqual(1, result.ImageCount);
            Assert.AreEqual(2, result.FileManifest.Count);
            Assert.IsTrue(result.FileManifest.ContainsKey("images" + Path.DirectorySeparatorChar + "image.png"));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ThrowingRecoveryDataReader(Exception failure) : IRecoveryDataReader
    {
        public Task<RecoveryDataSummary> ReadAsync(string dataDirectory, CancellationToken cancellationToken) =>
            Task.FromException<RecoveryDataSummary>(failure);
    }

    private sealed class FixedRecoveryDataReader(int historyCount, int todoCount, int noteCount, int imageCount) : IRecoveryDataReader
    {
        public Task<RecoveryDataSummary> ReadAsync(string dataDirectory, CancellationToken cancellationToken) =>
            Task.FromResult(new RecoveryDataSummary(historyCount, todoCount, noteCount, imageCount));
    }
}
