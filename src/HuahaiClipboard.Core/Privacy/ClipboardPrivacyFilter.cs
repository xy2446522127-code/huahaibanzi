namespace HuahaiClipboard.Core.Privacy;

public sealed class ClipboardPrivacyFilter
{
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
