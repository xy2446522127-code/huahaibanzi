using System;
using System.IO;

public static class InstallerLogPolicy
{
    public static string ResolvePath(string temporaryRoot, DateTime timestamp)
    {
        if (String.IsNullOrWhiteSpace(temporaryRoot))
            throw new ArgumentException("Temporary root is required.", "temporaryRoot");
        return Path.Combine(
            Path.GetFullPath(temporaryRoot),
            "HuahaiClipboard",
            "Installer",
            "install-" + timestamp.ToString("yyyyMMdd-HHmmssfff") + ".log");
    }
}
