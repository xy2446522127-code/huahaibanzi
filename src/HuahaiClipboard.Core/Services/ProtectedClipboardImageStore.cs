using System.Text;
using HuahaiClipboard.Core.Contracts;

namespace HuahaiClipboard.Core.Services;

public sealed class ProtectedClipboardImageStore(
    string imageDirectory,
    IBinaryProtector protector) : IClipboardImageStore
{
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("HHC1");

    public async Task<string> SaveAsync(
        string fileName,
        byte[] pngBytes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(pngBytes);
        Directory.CreateDirectory(imageDirectory);

        var path = CreateAvailablePath(fileName);
        await WriteProtectedAsync(path, pngBytes, cancellationToken);
        return path;
    }

    public async Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken)
    {
        var storedBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        return HasHeader(storedBytes)
            ? protector.Unprotect(storedBytes[Header.Length..])
            : storedBytes;
    }

    public async Task ProtectLegacyFilesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(imageDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(imageDirectory, "*.png"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storedBytes = await File.ReadAllBytesAsync(path, cancellationToken);
            if (!HasHeader(storedBytes))
            {
                await WriteProtectedAsync(path, storedBytes, cancellationToken);
            }
        }
    }

    private async Task WriteProtectedAsync(
        string path,
        byte[] plainBytes,
        CancellationToken cancellationToken)
    {
        var protectedBytes = protector.Protect(plainBytes);
        var storedBytes = new byte[Header.Length + protectedBytes.Length];
        Header.CopyTo(storedBytes, 0);
        protectedBytes.CopyTo(storedBytes, Header.Length);

        var temporaryPath = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, storedBytes, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string CreateAvailablePath(string fileName)
    {
        var path = Path.Combine(imageDirectory, fileName);
        if (!File.Exists(path))
        {
            return path;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 2; ; suffix++)
        {
            path = Path.Combine(imageDirectory, $"{stem}-{suffix}{extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }
    }

    private static bool HasHeader(byte[] value) =>
        value.Length >= Header.Length && value.AsSpan(0, Header.Length).SequenceEqual(Header);
}
