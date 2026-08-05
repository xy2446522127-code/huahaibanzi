using System;
using System.IO;

// 安装位置只允许本地磁盘下的具体目录；拒绝相对路径、UNC 与盘符根目录。
internal static class InstallLocationPolicy
{
    internal static string DefaultForRoots(string[] roots, string productFolderName)
    {
        if (roots == null || String.IsNullOrWhiteSpace(productFolderName))
            throw new ArgumentException("无法确定默认安装目录。");

        foreach (string candidate in roots)
        {
            if (String.IsNullOrWhiteSpace(candidate))
                continue;
            string root = Path.GetPathRoot(Path.GetFullPath(candidate));
            if (!IsForbiddenDrive(root))
                return Path.Combine(root, productFolderName);
        }

        throw new InvalidOperationException("未找到可用的非 C 盘。请先准备 D、E、F 等本地磁盘后再安装。");
    }

    internal static string Resolve(string requestedPath, string defaultPath)
    {
        if (String.IsNullOrWhiteSpace(defaultPath))
            throw new ArgumentException("默认安装目录无效。", "defaultPath");

        string selectedPath = String.IsNullOrWhiteSpace(requestedPath) ? defaultPath : requestedPath.Trim();
        if (!Path.IsPathRooted(selectedPath) || selectedPath.StartsWith("\\", StringComparison.Ordinal))
            throw new ArgumentException("安装目录必须是本地磁盘中的绝对路径。", "requestedPath");

        string fullPath = Path.GetFullPath(selectedPath);
        string root = Path.GetPathRoot(fullPath);
        if (String.IsNullOrWhiteSpace(root) || String.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("安装目录不能是磁盘根目录。", "requestedPath");
        if (IsForbiddenDrive(root))
            throw new ArgumentException("花海剪贴板不能安装到 C 盘，请选择 D、E、F 等其他本地磁盘。", "requestedPath");

        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsForbiddenDrive(string root)
    {
        return String.Equals(root, @"C:\", StringComparison.OrdinalIgnoreCase);
    }
}
