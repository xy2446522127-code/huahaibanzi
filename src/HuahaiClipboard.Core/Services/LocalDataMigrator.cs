using System.Security.Cryptography;

namespace HuahaiClipboard.Core.Services;

public enum LocalDataMigrationResult
{
    NotNeeded,
    TargetAlreadyExists,
    Migrated
}

public static class LocalDataMigrator
{
    public static LocalDataMigrationResult MigrateIfNeeded(
        LocalDataLayout layout,
        string legacyDirectory)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyDirectory);

        var source = Path.GetFullPath(legacyDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(source))
        {
            return LocalDataMigrationResult.NotNeeded;
        }

        if (Directory.Exists(layout.DataDirectory))
        {
            return LocalDataMigrationResult.TargetAlreadyExists;
        }

        Directory.CreateDirectory(layout.DataRoot);
        var temporary = layout.DataDirectory + ".migration-" + Guid.NewGuid().ToString("N");
        try
        {
            CopyDirectory(source, temporary, skipTopLevelUpdates: true);
            AssertEquivalent(source, temporary, skipTopLevelUpdates: true);
            Directory.Move(temporary, layout.DataDirectory);
            AssertEquivalent(source, layout.DataDirectory, skipTopLevelUpdates: true);
            Directory.Delete(source, recursive: true);
            return LocalDataMigrationResult.Migrated;
        }
        catch
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true);
            throw;
        }
    }

    private static void CopyDirectory(string source, string destination, bool skipTopLevelUpdates)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(directory);
            var relative = Path.GetRelativePath(source, directory);
            if (skipTopLevelUpdates && IsUpdatesPath(relative)) continue;
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(file);
            var relative = Path.GetRelativePath(source, file);
            if (skipTopLevelUpdates && IsUpdatesPath(relative)) continue;
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static void AssertEquivalent(string source, string destination, bool skipTopLevelUpdates)
    {
        var sourceFiles = EnumerateManifest(source, skipTopLevelUpdates);
        var destinationFiles = EnumerateManifest(destination, skipTopLevelUpdates: false);
        if (sourceFiles.Count != destinationFiles.Count)
        {
            throw new IOException("旧版数据迁移校验失败：文件数量不一致。");
        }

        foreach (var pair in sourceFiles)
        {
            if (!destinationFiles.TryGetValue(pair.Key, out var destinationHash) ||
                !pair.Value.SequenceEqual(destinationHash))
            {
                throw new IOException($"旧版数据迁移校验失败：{pair.Key}");
            }
        }
    }

    private static Dictionary<string, byte[]> EnumerateManifest(string root, bool skipTopLevelUpdates)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(file);
            var relative = Path.GetRelativePath(root, file);
            if (skipTopLevelUpdates && IsUpdatesPath(relative)) continue;
            using var stream = File.OpenRead(file);
            result.Add(relative, SHA256.HashData(stream));
        }

        return result;
    }

    private static bool IsUpdatesPath(string relative) =>
        relative.Equals("Updates", StringComparison.OrdinalIgnoreCase) ||
        relative.StartsWith("Updates" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("数据目录包含不受支持的链接，已停止迁移。");
        }
    }
}
