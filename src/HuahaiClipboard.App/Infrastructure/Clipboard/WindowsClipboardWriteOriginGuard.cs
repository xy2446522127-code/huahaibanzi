using System.Runtime.InteropServices;
using HuahaiClipboard.Core.Contracts;
using HuahaiClipboard.Core.Services;
using FormsClipboard = System.Windows.Forms.Clipboard;

namespace HuahaiClipboard.App.Infrastructure.Clipboard;

public sealed class WindowsClipboardWriteOriginGuard : IClipboardWriteOriginGuard
{
    public const string PrivateFormat = "HuahaiClipboard.InternalOrigin.v1";

    private readonly ClipboardWriteOriginState state = new();

    public string MarkerFormat => PrivateFormat;

    public string MarkerValue => state.Token;

    public bool IsCurrentWrite()
    {
        var dataObject = FormsClipboard.GetDataObject();
        var marker = dataObject?.GetDataPresent(MarkerFormat, autoConvert: false) == true
            ? dataObject.GetData(MarkerFormat, autoConvert: false) as string
            : null;
        return state.Matches(marker, GetClipboardSequenceNumber());
    }

    public void RecordSuccessfulWrite() => state.Record(GetClipboardSequenceNumber());

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
