using System;
using System.IO;

internal static class InstallTargetPolicy
{
    private const string ProductFolderName = "HuahaiClipboard";
    private const string OwnerMarkerName = ".huahai-install-owner";
    private static readonly string[] LegacyOwnershipFiles =
    {
        "HuahaiClipboard.exe",
        "HuahaiClipboard.App.dll",
        "Uninstall.ps1"
    };

    internal static void Validate(string installRoot, string registeredInstallRoot, string dataRoot)
    {
        string target = Normalize(installRoot, "安装目录无效。");
        string data = Normalize(dataRoot, "数据目录无效。");
        if (!String.Equals(Path.GetFileName(target), ProductFolderName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("安装目录必须以 HuahaiClipboard 命名。");

        if (PathsOverlap(target, data))
            throw new InvalidOperationException("安装目录不能与花海剪贴板的数据目录重叠。");

        if (!Directory.Exists(target) || Directory.GetFileSystemEntries(target).Length == 0)
            return;

        if (String.IsNullOrWhiteSpace(registeredInstallRoot) ||
            !SamePath(target, Normalize(registeredInstallRoot, "已注册安装目录无效。")))
        {
            throw new InvalidOperationException("所选目录不是已注册的花海剪贴板安装目录，已拒绝覆盖其中的文件。");
        }

        if (File.Exists(Path.Combine(target, OwnerMarkerName)))
            return;

        foreach (string fileName in LegacyOwnershipFiles)
        {
            if (!File.Exists(Path.Combine(target, fileName)))
                throw new InvalidOperationException("已注册目录缺少花海剪贴板程序文件，已拒绝覆盖。");
        }
    }

    internal static void WriteOwnerMarker(string installRoot)
    {
        string target = Normalize(installRoot, "安装目录无效。");
        File.WriteAllText(Path.Combine(target, OwnerMarkerName), "HuahaiClipboard\r\n");
    }

    private static string Normalize(string path, string message)
    {
        if (String.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(message);
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathsOverlap(string first, string second)
    {
        return SamePath(first, second) || IsAncestor(first, second) || IsAncestor(second, first);
    }

    private static bool IsAncestor(string parent, string child)
    {
        string prefix = parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePath(string first, string second)
    {
        return String.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}
