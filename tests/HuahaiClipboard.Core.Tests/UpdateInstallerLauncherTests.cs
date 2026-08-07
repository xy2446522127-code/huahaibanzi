using HuahaiClipboard.Core.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HuahaiClipboard.Core.Tests;

[TestClass]
public sealed class UpdateInstallerLauncherTests
{
    [TestMethod]
    public void BuildsSilentUpgradeCommandForTheCurrentInstallDirectory()
    {
        var installer = Path.Combine(Path.GetTempPath(), "HuahaiClipboard-Setup.exe");
        var installRoot = @"F:\HuahaiClipboard";

        var startInfo = UpdateInstallerLauncher.CreateStartInfo(installer, installRoot);

        Assert.AreEqual(Path.GetFullPath(installer), startInfo.FileName);
        Assert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(installer)), startInfo.WorkingDirectory);
        Assert.IsTrue(startInfo.UseShellExecute);
        CollectionAssert.AreEqual(
            new[] { "--silent", "--install-dir", installRoot },
            startInfo.ArgumentList.ToArray());
    }

    [TestMethod]
    public void RejectsAnUnexpectedExecutableName()
    {
        var installer = Path.Combine(Path.GetTempPath(), "another-program.exe");

        Assert.ThrowsException<InvalidDataException>(() =>
            UpdateInstallerLauncher.CreateStartInfo(installer, @"F:\HuahaiClipboard"));
    }

    [TestMethod]
    public void RejectsAnUnsignedInstallerBeforeStartingAProcess()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"HuahaiClipboard.UnsignedUpdateTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var installer = Path.Combine(directory, "HuahaiClipboard-Setup.exe");
        File.WriteAllText(installer, "unsigned fixture");
        try
        {
            var error = Assert.ThrowsException<InvalidDataException>(() =>
                UpdateInstallerLauncher.Start(installer, @"F:\HuahaiClipboard"));
            StringAssert.Contains(error.Message, "publisher signature");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
