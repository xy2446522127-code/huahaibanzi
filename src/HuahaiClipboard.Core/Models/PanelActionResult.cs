namespace HuahaiClipboard.Core.Models;

public sealed record PanelActionResult(bool Succeeded, string? RecoveryMessage)
{
    public static PanelActionResult Success() => new(true, null);

    public static PanelActionResult Failure(string message) => new(false, message);
}
