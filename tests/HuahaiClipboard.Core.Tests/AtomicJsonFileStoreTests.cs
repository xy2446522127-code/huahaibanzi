using System.Text.Json;
using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class AtomicJsonFileStoreTests
{
    [TestMethod]
    public async Task WriteVerifiedAsync_RetainsTwoPreviousVerifiedCopies()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-atomic-json-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var store = new AtomicJsonFileStore();
            await store.WriteVerifiedAsync(path, new[] { "one" }, Serialize, Deserialize, Validate, CancellationToken.None);
            await store.WriteVerifiedAsync(path, new[] { "two" }, Serialize, Deserialize, Validate, CancellationToken.None);
            await store.WriteVerifiedAsync(path, new[] { "three" }, Serialize, Deserialize, Validate, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "three" }, await ReadAsync(path));
            CollectionAssert.AreEqual(new[] { "two" }, await ReadAsync(path + ".bak1"));
            CollectionAssert.AreEqual(new[] { "one" }, await ReadAsync(path + ".bak2"));
            Assert.AreEqual(0, Directory.GetFiles(directory, "*.tmp").Length);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteVerifiedAsync_LeavesCurrentFileUntouchedWhenTemporaryContentFailsValidation()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"huahai-atomic-json-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, "[\"stable\"]");
            var store = new AtomicJsonFileStore();

            await Assert.ThrowsExceptionAsync<InvalidDataException>(() =>
                store.WriteVerifiedAsync(path, new[] { "bad" }, Serialize, Deserialize, _ => throw new InvalidDataException("reject"), CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "stable" }, await ReadAsync(path));
            Assert.IsFalse(File.Exists(path + ".bak1"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static string Serialize(string[] value) => JsonSerializer.Serialize(value);

    private static string[] Deserialize(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static void Validate(string[] value)
    {
        if (value.Length != 1) throw new InvalidDataException("Expected one value.");
    }

    private static async Task<string[]> ReadAsync(string path) => Deserialize(await File.ReadAllTextAsync(path));
}
