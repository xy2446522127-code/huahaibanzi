using HuahaiClipboard.Core.Recovery;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class RecoveryTransactionTests
{
    [TestMethod]
    public async Task ApplyAsync_RestoresDestinationWhenCandidateValidationFails()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-transaction-{Guid.NewGuid():N}");
        var destination = Path.Combine(root, "destination");
        var candidate = Path.Combine(root, "candidate");
        try
        {
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory(candidate);
            await File.WriteAllTextAsync(Path.Combine(destination, "history.dat"), "old-history");
            await File.WriteAllTextAsync(Path.Combine(candidate, "history.dat"), "new-history");

            var result = await new RecoveryTransaction().ApplyAsync(
                candidate,
                destination,
                _ => Task.FromException(new InvalidDataException("candidate is not a superset")),
                CancellationToken.None);

            Assert.AreEqual(RecoveryTransactionState.RolledBack, result.State);
            Assert.AreEqual("old-history", await File.ReadAllTextAsync(Path.Combine(destination, "history.dat")));
            Assert.IsTrue(Directory.Exists(result.PreservedCandidateRoot));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ApplyAsync_ActivatesCandidateOnlyAfterValidationSucceeds()
    {
        var root = Path.Combine(Path.GetTempPath(), $"huahai-recovery-transaction-{Guid.NewGuid():N}");
        var destination = Path.Combine(root, "destination");
        var candidate = Path.Combine(root, "candidate");
        try
        {
            Directory.CreateDirectory(destination);
            Directory.CreateDirectory(candidate);
            await File.WriteAllTextAsync(Path.Combine(destination, "history.dat"), "old-history");
            await File.WriteAllTextAsync(Path.Combine(candidate, "history.dat"), "new-history");

            var result = await new RecoveryTransaction().ApplyAsync(
                candidate,
                destination,
                _ => Task.CompletedTask,
                CancellationToken.None);

            Assert.AreEqual(RecoveryTransactionState.Activated, result.State);
            Assert.AreEqual("new-history", await File.ReadAllTextAsync(Path.Combine(destination, "history.dat")));
            Assert.IsTrue(Directory.Exists(result.PreservedPreviousRoot));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
