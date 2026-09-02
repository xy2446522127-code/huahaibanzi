using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

internal static class UpgradePreflightPolicy
{
    internal static string CreateVerifiedSnapshot(string sourceRoot, string snapshotParent)
    {
        var source = NormalizeExisting(sourceRoot);
        var parent = Normalize(snapshotParent);
        if (IsNested(source, parent) || IsNested(parent, source))
            throw new InvalidOperationException("Snapshot and source paths must not overlap.");

        Directory.CreateDirectory(parent);
        var snapshot = Path.Combine(parent, "snapshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(source, snapshot);
            var sourceManifest = CreateManifest(source);
            var copyManifest = CreateManifest(snapshot);
            AssertEquivalent(sourceManifest, copyManifest);
            File.WriteAllLines(Path.Combine(snapshot, "manifest.sha256"),
                copyManifest.Select(pair => pair.Value + " *" + pair.Key).ToArray());
            return snapshot;
        }
        catch
        {
            if (Directory.Exists(snapshot)) Directory.Delete(snapshot, true);
            throw;
        }
    }

    internal static bool VerifySnapshot(string snapshotRoot)
    {
        var snapshot = NormalizeExisting(snapshotRoot);
        var manifestPath = Path.Combine(snapshot, "manifest.sha256");
        if (!File.Exists(manifestPath)) return false;
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(manifestPath))
        {
            var separator = line.IndexOf(" *", StringComparison.Ordinal);
            if (separator <= 0) return false;
            expected[line.Substring(separator + 2)] = line.Substring(0, separator);
        }

        var actual = CreateManifest(snapshot, excludeManifest: true);
        if (expected.Count != actual.Count) return false;
        foreach (var pair in expected)
        {
            string hash;
            if (!actual.TryGetValue(pair.Key, out hash) ||
                !String.Equals(pair.Value, hash, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static Dictionary<string, string> CreateManifest(string root, bool excludeManifest = false)
    {
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            if (excludeManifest && String.Equals(Path.GetFileName(file), "manifest.sha256", StringComparison.OrdinalIgnoreCase))
                continue;
            RejectReparsePoint(file);
            using (var stream = File.OpenRead(file))
                manifest[RelativePath(root, file)] = ToHex(SHA256.Create().ComputeHash(stream));
        }
        return manifest;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(directory);
            Directory.CreateDirectory(Path.Combine(destination, RelativePath(source, directory)));
        }
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(file);
            var target = Path.Combine(destination, RelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target);
        }
    }

    private static void AssertEquivalent(Dictionary<string, string> source, Dictionary<string, string> copy)
    {
        if (source.Count != copy.Count) throw new IOException("Snapshot file count mismatch.");
        foreach (var pair in source)
        {
            string hash;
            if (!copy.TryGetValue(pair.Key, out hash) ||
                !String.Equals(pair.Value, hash, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Snapshot hash mismatch: " + pair.Key);
        }
    }

    private static string NormalizeExisting(string path)
    {
        var normalized = Normalize(path);
        if (!Directory.Exists(normalized)) throw new DirectoryNotFoundException(normalized);
        return normalized;
    }

    private static string Normalize(string path)
    {
        if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.");
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool IsNested(string parent, string child)
    {
        var prefix = parent + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string ToHex(byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", String.Empty);
    }

    private static string RelativePath(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Path escaped snapshot root.");
        return path.Substring(prefix.Length);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Snapshot source contains an unsupported link.");
    }
}
