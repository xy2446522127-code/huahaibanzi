using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class DataLocationRegistryTests
{
    [TestMethod]
    public async Task ResolveDataRootAsync_UsesRegisteredStableRootBeforePopulatedInstallRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-data-root-{Guid.NewGuid():N}");
        var registeredDataRoot = Path.Combine(root, "stable-data");
        var installRoot = Path.Combine(root, "program", "HuahaiClipboard");
        try
        {
            WriteRecognizedData(registeredDataRoot, "S-1-5-21-registered");
            Directory.CreateDirectory(Path.Combine(installRoot, "Data"));

            var result = await LocalDataLayout.ResolveDataRootAsync(
                new MemoryDataLocationRegistry(registeredDataRoot),
                installRoot,
                Path.Combine(root, "legacy"),
                CancellationToken.None);

            Assert.AreEqual(DataRootResolutionKind.Registered, result.Kind);
            Assert.AreEqual(registeredDataRoot, result.DataRoot, ignoreCase: true);
            Assert.AreEqual(0, result.ConflictingDataRoots.Count);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ResolveDataRootAsync_RequiresRecoveryWhenRegisteredAndInstallRootsAreBothPopulated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-data-root-{Guid.NewGuid():N}");
        var registeredDataRoot = Path.Combine(root, "stable-data");
        var installRoot = Path.Combine(root, "program", "HuahaiClipboard");
        try
        {
            WriteRecognizedData(registeredDataRoot, "S-1-5-21-registered");
            WriteRecognizedData(Path.Combine(installRoot, "Data"), "S-1-5-21-install");

            var result = await LocalDataLayout.ResolveDataRootAsync(
                new MemoryDataLocationRegistry(registeredDataRoot),
                installRoot,
                Path.Combine(root, "legacy"),
                CancellationToken.None);

            Assert.AreEqual(DataRootResolutionKind.RecoveryRequired, result.Kind);
            Assert.IsNull(result.DataRoot);
            CollectionAssert.AreEquivalent(
                new[] { registeredDataRoot, Path.Combine(installRoot, "Data") },
                result.ConflictingDataRoots.ToArray(),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ResolveDataRootAsync_PreservesLegacyDirectoryAsMigrationCandidateInsteadOfDataRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-data-root-{Guid.NewGuid():N}");
        var installRoot = Path.Combine(root, "program", "HuahaiClipboard");
        var legacyRoot = Path.Combine(root, "legacy");
        try
        {
            Directory.CreateDirectory(legacyRoot);
            await File.WriteAllTextAsync(Path.Combine(legacyRoot, "history.dat"), "legacy-data");

            var result = await LocalDataLayout.ResolveDataRootAsync(
                new MemoryDataLocationRegistry(null),
                installRoot,
                legacyRoot,
                CancellationToken.None);

            Assert.AreEqual(DataRootResolutionKind.NewInstall, result.Kind);
            Assert.AreEqual(Path.Combine(installRoot, "Data"), result.DataRoot, ignoreCase: true);
            Assert.AreEqual(legacyRoot, result.LegacyMigrationSource, ignoreCase: true);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteRecognizedData(string dataRoot, string userKey)
    {
        var userDirectory = Path.Combine(dataRoot, userKey);
        Directory.CreateDirectory(userDirectory);
        File.WriteAllText(Path.Combine(userDirectory, "history.dat"), "fixture");
    }

    private sealed class MemoryDataLocationRegistry(string? dataRoot) : IDataLocationRegistry
    {
        private string? dataRoot = dataRoot;

        public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(dataRoot);

        public Task WriteAsync(string value, CancellationToken cancellationToken)
        {
            dataRoot = value;
            return Task.CompletedTask;
        }
    }
}
