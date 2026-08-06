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
}
