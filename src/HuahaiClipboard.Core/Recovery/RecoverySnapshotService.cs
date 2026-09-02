using System.Security.Cryptography;
using System.Text.Json;

namespace HuahaiClipboard.Core.Recovery;

public sealed record RecoverySnapshotRequest(string SnapshotParent, string Name);

public sealed record RecoverySnapshot(
    string SnapshotRoot,
    string ManifestPath,
    IReadOnlyDictionary<string, string> SourceManifest,
    IReadOnlyDictionary<string, string> CopyManifest);

public sealed class RecoverySnapshotService
{
    public async Task<RecoverySnapshot> CreateAsync(
        RecoverySource source,
        RecoverySnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            request.Name.Contains(Path.DirectorySeparatorChar) ||
            request.Name.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Snapshot name is invalid.", nameof(request));
        }

        var sourceRoot = NormalizeDirectory(source.Root);
        var snapshotParent = NormalizeDirectory(request.SnapshotParent);
        EnsureSeparate(sourceRoot, snapshotParent);
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);

        var sourceManifest = CreateManifest(sourceRoot, cancellationToken);
        Directory.CreateDirectory(snapshotParent);
        var snapshotRoot = Path.Combine(snapshotParent, $"{request.Name}-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(sourceRoot, snapshotRoot, cancellationToken);
            var copyManifest = CreateManifest(snapshotRoot, cancellationToken);
            AssertEquivalent(sourceManifest, copyManifest);
            var manifestPath = Path.Combine(snapshotRoot, "recovery-snapshot-manifest.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new RecoverySnapshotManifest(sourceManifest)),
                cancellationToken);
            return new RecoverySnapshot(snapshotRoot, manifestPath, sourceManifest, copyManifest);
        }
        catch
        {
            if (Directory.Exists(snapshotRoot)) Directory.Delete(snapshotRoot, recursive: true);
            throw;
        }
    }

    private static void CopyDirectory(string sourceRoot, string destinationRoot, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationRoot);
        foreach (var directory in Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            var target = Path.Combine(destinationRoot, Path.GetRelativePath(sourceRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static IReadOnlyDictionary<string, string> CreateManifest(string root, CancellationToken cancellationToken)
    {
        var manifest = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RejectReparsePoint(file);
            using var stream = File.OpenRead(file);
            manifest.Add(Path.GetRelativePath(root, file), Convert.ToHexString(SHA256.HashData(stream)));
        }

        return manifest;
    }

    private static void AssertEquivalent(
        IReadOnlyDictionary<string, string> source,
        IReadOnlyDictionary<string, string> copy)
    {
        if (source.Count != copy.Count) throw new IOException("Snapshot file count mismatch.");
        foreach (var pair in source)
        {
            if (!copy.TryGetValue(pair.Key, out var hash) ||
                !string.Equals(pair.Value, hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Snapshot hash mismatch: " + pair.Key);
            }
        }
    }

    private static void EnsureSeparate(string sourceRoot, string snapshotParent)
    {
        var sourcePrefix = sourceRoot + Path.DirectorySeparatorChar;
        var snapshotPrefix = snapshotParent + Path.DirectorySeparatorChar;
        if (string.Equals(sourceRoot, snapshotParent, StringComparison.OrdinalIgnoreCase) ||
            snapshotPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase) ||
            sourcePrefix.StartsWith(snapshotPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Snapshot parent must not overlap the recovery source.");
        }
    }

    private static string NormalizeDirectory(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Recovery source contains an unsupported link.");
        }
    }

    private sealed record RecoverySnapshotManifest(IReadOnlyDictionary<string, string> Files);
}
