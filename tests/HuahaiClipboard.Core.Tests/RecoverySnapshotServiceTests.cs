using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoverySnapshotServiceTests
{
    [TestMethod]
    public async Task CreateAsync_CopiesEverySourceFileAndWritesAnEquivalentHashManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-snapshot-{Guid.NewGuid():N}");
        var sourceRoot = Path.Combine(root, "source");
        var snapshotParent = Path.Combine(root, "snapshots");
        try
        {
            Directory.CreateDirectory(Path.Combine(sourceRoot, "images"));
            await File.WriteAllTextAsync(Path.Combine(sourceRoot, "history.dat"), "history");
            await File.WriteAllTextAsync(Path.Combine(sourceRoot, "images", "image.png"), "image");

            var snapshot = await new RecoverySnapshotService().CreateAsync(
                new RecoverySource(sourceRoot, RecoverySourceKind.InstallDataRoot, "fixture"),
                new RecoverySnapshotRequest(snapshotParent, "before-upgrade"),
                CancellationToken.None);

            Assert.IsTrue(File.Exists(snapshot.ManifestPath));
            Assert.AreEqual(2, snapshot.SourceManifest.Count);
            CollectionAssert.AreEquivalent(
                snapshot.SourceManifest.Keys.ToArray(),
                snapshot.CopyManifest.Keys.ToArray());
            foreach (var pair in snapshot.SourceManifest)
            {
                Assert.AreEqual(pair.Value, snapshot.CopyManifest[pair.Key]);
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task CreateAsync_RejectsSnapshotParentInsideTheSourceDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-snapshot-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "history.dat"), "history");

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                new RecoverySnapshotService().CreateAsync(
                    new RecoverySource(root, RecoverySourceKind.InstallDataRoot, "fixture"),
                    new RecoverySnapshotRequest(Path.Combine(root, "snapshots"), "before-upgrade"),
                    CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
