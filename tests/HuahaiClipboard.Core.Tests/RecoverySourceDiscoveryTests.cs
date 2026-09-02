using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoverySourceDiscoveryTests
{
    [TestMethod]
    public void Discover_DeduplicatesNormalizedExistingDirectoriesWhileKeepingFirstProvenance()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-discovery-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "Data", "S-1-5-21-1000");
        try
        {
            Directory.CreateDirectory(source);

            var results = RecoverySourceDiscovery.Discover(
            [
                new RecoveryDiscoveryHint(source, RecoverySourceKind.InstallDataRoot, "installed program"),
                new RecoveryDiscoveryHint(source + Path.DirectorySeparatorChar, RecoverySourceKind.Backup, "backup sibling")
            ]);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Path.GetFullPath(source), results[0].Root, ignoreCase: true);
            Assert.AreEqual(RecoverySourceKind.InstallDataRoot, results[0].Kind);
            Assert.AreEqual("installed program", results[0].Provenance);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Discover_SkipsNonexistentAndRegularFileHints()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-discovery-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var regularFile = Path.Combine(root, "history.dat");
            File.WriteAllText(regularFile, "fixture");

            var results = RecoverySourceDiscovery.Discover(
            [
                new RecoveryDiscoveryHint(Path.Combine(root, "missing"), RecoverySourceKind.Backup, "missing"),
                new RecoveryDiscoveryHint(regularFile, RecoverySourceKind.CorruptHistory, "file")
            ]);

            Assert.AreEqual(0, results.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
