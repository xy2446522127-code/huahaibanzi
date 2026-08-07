using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

internal static class InstallDataPreserver
{
    internal static void CopyIntoCandidate(string installRoot, string stagingRoot)
    {
        string source = Path.Combine(Path.GetFullPath(installRoot), "Data");
        if (!Directory.Exists(source)) return;

        string destination = Path.Combine(Path.GetFullPath(stagingRoot), "Data");
        if (Directory.Exists(destination) || File.Exists(destination))
            throw new InvalidDataException("The candidate package unexpectedly contains a Data path.");

        CopyDirectory(source, destination);
        AssertEquivalent(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(directory);
            Directory.CreateDirectory(Path.Combine(destination, RelativePath(source, directory)));
        }

        foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(file);
            string target = Path.Combine(destination, RelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(file, target, false);
        }
    }

    private static void AssertEquivalent(string source, string destination)
    {
        Dictionary<string, string> sourceManifest = Manifest(source);
        Dictionary<string, string> destinationManifest = Manifest(destination);
        if (sourceManifest.Count != destinationManifest.Count)
            throw new IOException("Install-root data preservation failed: file count mismatch.");

        foreach (KeyValuePair<string, string> pair in sourceManifest)
        {
            string value;
            if (!destinationManifest.TryGetValue(pair.Key, out value) ||
                !String.Equals(pair.Value, value, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Install-root data preservation failed: " + pair.Key);
        }
    }

    private static Dictionary<string, string> Manifest(string root)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            RejectReparsePoint(file);
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(file))
                result.Add(RelativePath(root, file), Convert.ToBase64String(sha256.ComputeHash(stream)));
        }
        return result;
    }

    private static string RelativePath(string root, string path)
    {
        string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Data path escapes the installation root.");
        return fullPath.Substring(prefix.Length);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Install-root data contains an unsupported link.");
    }
}
