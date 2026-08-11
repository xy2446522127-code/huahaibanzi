namespace HuahaiClipboard.Core.Contracts;

public interface IClipboardWriteOriginGuard
{
    string MarkerFormat { get; }

    string MarkerValue { get; }

    bool IsCurrentWrite();

    void ExecuteOwnedWrite(Action write);
}
