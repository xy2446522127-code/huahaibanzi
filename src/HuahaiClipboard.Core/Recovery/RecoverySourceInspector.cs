using System.Security.Cryptography;
using System.Text.Json;

namespace HuahaiClipboard.Core.Recovery;

public sealed class RecoverySourceInspector(IRecoveryDataReader reader)
{
    private readonly IRecoveryDataReader reader = reader ?? throw new ArgumentNullException(nameof(reader));

    public async Task<RecoveryInspection> InspectAsync(
        RecoverySource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        IReadOnlyDictionary<string, string> manifest;
        try
        {
            manifest = CreateManifest(source.Root, cancellationToken);
        }
        catch (DirectoryNotFoundException)
        {
            return CreateFailure(source, RecoveryInspectionState.Unavailable, "source-directory-missing");
        }
        catch (UnauthorizedAccessException)
        {
            return CreateFailure(source, RecoveryInspectionState.Unavailable, "source-access-denied");
        }
        catch (IOException)
        {
            return CreateFailure(source, RecoveryInspectionState.Unavailable, "source-read-failed");
        }

        try
        {
            var summary = await reader.ReadAsync(source.Root, cancellationToken);
            return new RecoveryInspection(
                source,
                RecoveryInspectionState.Readable,
                summary.HistoryCount,
                summary.TodoCount,
                summary.NoteCount,
                summary.ImageCount,
                manifest,
                null);
        }
        catch (CryptographicException)
        {
            return CreateFailure(source, RecoveryInspectionState.EncryptedForAnotherUser, "dpapi-unavailable", manifest);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return CreateFailure(source, RecoveryInspectionState.Malformed, "data-malformed", manifest);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return CreateFailure(source, RecoveryInspectionState.Incomplete, "data-incomplete", manifest);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return CreateFailure(source, RecoveryInspectionState.Unavailable, "data-unavailable", manifest);
        }
    }

    private static RecoveryInspection CreateFailure(
        RecoverySource source,
        RecoveryInspectionState state,
        string errorCode,
        IReadOnlyDictionary<string, string>? manifest = null) =>
        new(source, state, 0, 0, 0, 0, manifest ?? new Dictionary<string, string>(), errorCode);

    private static IReadOnlyDictionary<string, string> CreateManifest(
        string root,
        CancellationToken cancellationToken)
    {
        var normalizedRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(normalizedRoot)) throw new DirectoryNotFoundException(normalizedRoot);
        var manifest = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(normalizedRoot, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Recovery source contains an unsupported link.");
            }

            var relative = Path.GetRelativePath(normalizedRoot, path);
            using var stream = File.OpenRead(path);
            manifest.Add(relative, Convert.ToHexString(SHA256.HashData(stream)));
        }

        return manifest;
    }
}
