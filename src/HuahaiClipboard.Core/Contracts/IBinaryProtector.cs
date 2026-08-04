namespace HuahaiClipboard.Core.Contracts;

public interface IBinaryProtector
{
    byte[] Protect(byte[] value);
    byte[] Unprotect(byte[] value);
}
