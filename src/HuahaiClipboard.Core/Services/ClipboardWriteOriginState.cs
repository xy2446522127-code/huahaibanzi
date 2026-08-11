namespace HuahaiClipboard.Core.Services;

public sealed class ClipboardWriteOriginState
{
    private long ownedSequence;

    public ClipboardWriteOriginState()
        : this(Guid.NewGuid().ToString("N"))
    {
    }

    public ClipboardWriteOriginState(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("The clipboard origin token must not be empty.", nameof(token));
        }

        Token = token;
    }

    public string Token { get; }

    public void Record(uint sequence)
    {
        if (sequence == 0)
        {
            return;
        }

        Interlocked.Exchange(ref ownedSequence, sequence);
    }

    public bool Matches(string? marker, uint sequence) =>
        sequence != 0 &&
        sequence == unchecked((uint)Interlocked.Read(ref ownedSequence)) &&
        string.Equals(marker, Token, StringComparison.Ordinal);
}
