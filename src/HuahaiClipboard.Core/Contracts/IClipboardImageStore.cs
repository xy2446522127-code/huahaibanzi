namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardImageStore
{
    Task<string> SaveAsync(string fileName, byte[] pngBytes, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken);
    Task ProtectLegacyFilesAsync(CancellationToken cancellationToken);
    Task DeleteAsync(string filePath, CancellationToken cancellationToken) => Task.CompletedTask;
    Task DeleteUnreferencedAsync(
        IReadOnlyCollection<string> referencedPaths,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
