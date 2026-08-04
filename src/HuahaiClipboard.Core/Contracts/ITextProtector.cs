namespace HuahaiClipboard.Core.Contracts;

public interface ITextProtector
{
    string Protect(string value);
    string Unprotect(string value);
}
