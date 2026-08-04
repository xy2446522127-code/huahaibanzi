using System.Security.Cryptography;
using System.Text;
using HuahaiClipboard.Core.Contracts;

namespace HuahaiClipboard.App.Infrastructure.Storage;

public sealed class DpapiTextProtector : ITextProtector, IBinaryProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("HuahaiClipboard.History.v1");

    public string Protect(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return Convert.ToBase64String(Protect(bytes));
    }

    public string Unprotect(string value)
    {
        var bytes = Convert.FromBase64String(value);
        return Encoding.UTF8.GetString(Unprotect(bytes));
    }

    public byte[] Protect(byte[] value) =>
        ProtectedData.Protect(value, Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] value) =>
        ProtectedData.Unprotect(value, Entropy, DataProtectionScope.CurrentUser);
}
