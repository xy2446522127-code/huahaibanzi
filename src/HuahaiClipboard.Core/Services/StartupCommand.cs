namespace HuahaiClipboard.Core.Services;

public static class StartupCommand
{
    public static string Create(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"\"{executablePath}\" --background";
    }
}

public static class StartupLaunchPolicy
{
    public static bool ShouldStartHidden(string? arguments) =>
        (arguments ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains("--background", StringComparer.OrdinalIgnoreCase);
}
