using System;
using System.IO;
using Microsoft.Win32;

internal static class DataLocationPolicy
{
    private const string RegistrySubKey = @"Software\HuahaiClipboard";
    private const string ValueName = "DataLocation";

    internal static string Resolve(string installRoot, string registeredDataRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            throw new ArgumentException("Install root is required.", "installRoot");

        if (!string.IsNullOrWhiteSpace(registeredDataRoot))
            return Normalize(registeredDataRoot);

        return Normalize(Path.Combine(installRoot, "Data"));
    }

    internal static string Resolve(string installRoot, string registeredDataRoot, string requestedDataRoot)
    {
        var resolved = Resolve(installRoot, registeredDataRoot);
        if (string.IsNullOrWhiteSpace(requestedDataRoot))
            return resolved;

        var requested = Normalize(requestedDataRoot);
        if (!String.Equals(requested, resolved, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Stable data location cannot change during a normal upgrade.");
        return resolved;
    }

    internal static string ReadRegistered()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(RegistrySubKey, writable: false))
            return key == null ? null : key.GetValue(ValueName) as string;
    }

    internal static void WriteRegistered(string dataRoot)
    {
        var normalized = Normalize(dataRoot);
        using (var key = Registry.CurrentUser.CreateSubKey(RegistrySubKey))
        {
            if (key == null) throw new InvalidOperationException("Unable to write stable data location.");
            key.SetValue(ValueName, normalized, RegistryValueKind.String);
        }
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
