namespace HuahaiClipboard.Core.Privacy;

using System.Text.RegularExpressions;

public sealed class ClipboardPrivacyFilter
{
    private static readonly Regex SensitiveContentPattern = new(
        @"(?ix)(?:\bsk-[a-z0-9_-]{32,}\b|\bgh[pousr]_[a-z0-9]{24,}\b|\bAKIA[0-9A-Z]{16}\b|-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----|\b(?:password|passwd|pwd|密码)\s*[:=：]\s*\S{6,}|\b(?:otp|one[- ]time\s+code|verification\s+code|验证码)\D{0,8}\d{4,8}\b)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly HashSet<string> PasswordManagers = new(StringComparer.OrdinalIgnoreCase)
    {
        "1password.exe",
        "bitwarden.exe",
        "dashlane.exe",
        "keepass.exe",
        "keepassxc.exe",
        "lastpass.exe",
        "nordpass.exe",
        "protonpass.exe",
        "roboform.exe"
    };

    private static readonly string[] PrivateWindowMarkers =
    [
        "incognito",
        "inprivate",
        "private browsing",
        "无痕",
        "隐私浏览"
    ];

    private readonly HashSet<string> excludedProcesses;

    public ClipboardPrivacyFilter(IEnumerable<string>? excludedProcesses = null)
    {
        this.excludedProcesses = new HashSet<string>(
            (excludedProcesses ?? []).Select(NormalizeProcessName).Where(value => value.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool ShouldExclude(string? processName, string? windowTitle)
    {
        var normalizedProcess = NormalizeProcessName(processName);
        if (PasswordManagers.Contains(normalizedProcess) || excludedProcesses.Contains(normalizedProcess))
        {
            return true;
        }

        return PrivateWindowMarkers.Any(marker =>
            (windowTitle ?? string.Empty).Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public bool ShouldExcludeContent(string text) =>
        !string.IsNullOrWhiteSpace(text) && SensitiveContentPattern.IsMatch(text);

    private static string NormalizeProcessName(string? processName)
    {
        var name = Path.GetFileName(processName ?? string.Empty).Trim();
        if (name.Length > 0 && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name += ".exe";
        }

        return name;
    }
}
