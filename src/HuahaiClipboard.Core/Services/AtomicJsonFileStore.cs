using System.Text;

namespace HuahaiClipboard.Core.Services;

public sealed class AtomicJsonFileStore
{
    public async Task WriteVerifiedAsync<T>(
        string path,
        T value,
        Func<T, string> serialize,
        Func<string, T> deserialize,
        Action<T> validate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(serialize);
        ArgumentNullException.ThrowIfNull(deserialize);
        ArgumentNullException.ThrowIfNull(validate);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Data file directory is invalid.");
        Directory.CreateDirectory(directory);
        var temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var serialized = serialize(value);
            await WriteAndFlushAsync(temporaryPath, serialized, cancellationToken);
            var verified = deserialize(await File.ReadAllTextAsync(temporaryPath, cancellationToken));
            validate(verified);
            RotateBackups(fullPath);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static async Task WriteAndFlushAsync(string path, string content, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(content), cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static void RotateBackups(string path)
    {
        if (!File.Exists(path)) return;
        var firstBackup = path + ".bak1";
        var secondBackup = path + ".bak2";
        if (File.Exists(firstBackup)) File.Move(firstBackup, secondBackup, overwrite: true);
        File.Copy(path, firstBackup, overwrite: true);
    }
}
