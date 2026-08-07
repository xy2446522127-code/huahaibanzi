using System.Diagnostics;

namespace HuahaiClipboard.Core.Services;

public static class UpdateInstallerLauncher
{
    public static ProcessStartInfo CreateStartInfo(string installerPath, string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

        var resolvedInstaller = Path.GetFullPath(installerPath);
        if (!string.Equals(
                Path.GetFileName(resolvedInstaller),
                GitHubUpdateCheckService.InstallerAssetName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("更新安装包名称无效。");
        }

        var resolvedInstallRoot = Path.GetFullPath(installRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(
                Path.GetFileName(resolvedInstallRoot),
                "HuahaiClipboard",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("当前安装目录无效。");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedInstaller,
            WorkingDirectory = Path.GetDirectoryName(resolvedInstaller),
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add("--silent");
        startInfo.ArgumentList.Add("--install-dir");
        startInfo.ArgumentList.Add(resolvedInstallRoot);
        return startInfo;
    }

    public static Process Start(string installerPath, string installRoot)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("已验证的更新安装包不存在。", installerPath);

        InstallerPublisherSignaturePolicy.Verify(
            installerPath,
            InstallerPublisherSignaturePolicy.PinnedPublisherThumbprint);

        return Process.Start(CreateStartInfo(installerPath, installRoot))
            ?? throw new InvalidOperationException("无法启动更新安装程序。");
    }
}
