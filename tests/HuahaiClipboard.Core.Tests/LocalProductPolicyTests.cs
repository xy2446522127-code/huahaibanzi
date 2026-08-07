using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class LocalProductPolicyTests
{
    [TestMethod]
    public void LocalDataLayout_UsesAnInstallRootChildIsolatedByWindowsUser()
    {
        var layout = new LocalDataLayout(@"F:\HuahaiClipboard", "S-1-5-21-1000");

        Assert.AreEqual(
            @"F:\HuahaiClipboard\Data\S-1-5-21-1000",
            layout.DataDirectory);
        Assert.AreEqual(
            @"F:\HuahaiClipboard\Data\S-1-5-21-1000\history.dat",
            layout.HistoryFile);
        Assert.AreEqual(
            @"F:\HuahaiClipboard\Data\S-1-5-21-1000\settings.json",
            layout.SettingsFile);
        Assert.AreEqual(
            @"F:\HuahaiClipboard\Data\S-1-5-21-1000\images",
            layout.ImageDirectory);
    }

    [TestMethod]
    public void LocalDataLayout_UsesExplicitInstallAndUserRootsWhenPresent()
    {
        var previousInstallRoot = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_INSTALL_ROOT");
        var previousUserKey = Environment.GetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY");
        try
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_INSTALL_ROOT", @"F:\Huahai-Isolated");
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", "test-user");

            Assert.AreEqual(@"F:\Huahai-Isolated", LocalDataLayout.ResolveInstallRoot());
            Assert.AreEqual("test-user", LocalDataLayout.ResolveUserKey());
        }
        finally
        {
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_INSTALL_ROOT", previousInstallRoot);
            Environment.SetEnvironmentVariable("HUAHAI_CLIPBOARD_USER_KEY", previousUserKey);
        }
    }

    [DataTestMethod]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("CON")]
    [DataRow("con.txt")]
    [DataRow("user.")]
    [DataRow("user ")]
    public void LocalDataLayout_UnsafeWindowsUserKeysRemainDirectChildrenOfDataRoot(string userKey)
    {
        var layout = new LocalDataLayout(@"F:\HuahaiClipboard", userKey);
        var resolvedDataRoot = Path.GetFullPath(layout.DataRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var resolvedDataDirectory = Path.GetFullPath(layout.DataDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        Assert.AreEqual(
            resolvedDataRoot,
            Path.GetDirectoryName(resolvedDataDirectory),
            ignoreCase: true);
        Assert.AreNotEqual(
            layout.InstallRoot,
            resolvedDataDirectory,
            ignoreCase: true);
        Assert.AreEqual("default-user", Path.GetFileName(resolvedDataDirectory));
    }

    [TestMethod]
    public void LocalDataMigrator_MovesKnownDataAndRemovesTheVerifiedLegacyDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"HuahaiClipboard.DataMigration.{Guid.NewGuid():N}");
        var installRoot = Path.Combine(root, "Install", "HuahaiClipboard");
        var legacyRoot = Path.Combine(root, "LocalAppData", "HuahaiClipboard");
        try
        {
            Directory.CreateDirectory(Path.Combine(legacyRoot, "images"));
            File.WriteAllText(Path.Combine(legacyRoot, "history.dat"), "history-v1");
            File.WriteAllText(Path.Combine(legacyRoot, "settings.json"), "settings-v1");
            File.WriteAllText(Path.Combine(legacyRoot, "window-positions.json"), "positions-v1");
            File.WriteAllText(Path.Combine(legacyRoot, "images", "image.bin"), "image-v1");
            Directory.CreateDirectory(Path.Combine(legacyRoot, "Updates"));
            File.WriteAllText(Path.Combine(legacyRoot, "Updates", "old-installer.exe"), "transient");

            var layout = new LocalDataLayout(installRoot, "S-1-5-21-1000");
            var result = LocalDataMigrator.MigrateIfNeeded(layout, legacyRoot);

            Assert.AreEqual(LocalDataMigrationResult.Migrated, result);
            Assert.AreEqual("history-v1", File.ReadAllText(layout.HistoryFile));
            Assert.AreEqual("settings-v1", File.ReadAllText(layout.SettingsFile));
            Assert.AreEqual("positions-v1", File.ReadAllText(layout.WindowPositionsFile));
            Assert.AreEqual("image-v1", File.ReadAllText(Path.Combine(layout.ImageDirectory, "image.bin")));
            Assert.IsFalse(Directory.Exists(Path.Combine(layout.DataDirectory, "Updates")));
            Assert.IsFalse(Directory.Exists(legacyRoot));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void UpdateCache_UsesSystemTemporaryStorageInsteadOfInstallRootData()
    {
        var pending = UpdateCacheLayout.ResolvePendingDirectory(
            @"C:\Users\TestUser\AppData\Local\Temp",
            "S-1-5-21-1000");

        Assert.AreEqual(
            @"C:\Users\TestUser\AppData\Local\Temp\HuahaiClipboard\Updates\S-1-5-21-1000\Pending",
            pending);
        Assert.IsFalse(pending.StartsWith(@"F:\HuahaiClipboard\Data", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ImageDisplayName_UsesAReadableTimestampInsteadOfDimensionsOrAnId()
    {
        var actual = ClipboardDisplayName.CreateImageFileName(
            DateTimeOffset.Parse("2026-08-04T16:42:05+08:00"));

        Assert.AreEqual("花海截图-20260804-164205.png", actual);
    }
}
