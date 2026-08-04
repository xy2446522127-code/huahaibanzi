namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardImageStore
{
    Task<string> SaveAsync(string fileName, byte[] pngBytes, CancellationToken cancellationToken);
    Task<byte[]> ReadAsync(string filePath, CancellationToken cancellationToken);
    Task ProtectLegacyFilesAsync(CancellationToken cancellationToken);
}
