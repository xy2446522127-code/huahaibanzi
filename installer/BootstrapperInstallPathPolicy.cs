using System;
using System.IO;

internal static class BootstrapperInstallPathPolicy
{
    internal static string Resolve(string requestedInstallRoot, string registeredInstallRoot, bool migrationMode)
    {
        if (!string.IsNullOrWhiteSpace(registeredInstallRoot))
        {
            var registered = Normalize(registeredInstallRoot);
            if (string.IsNullOrWhiteSpace(requestedInstallRoot))
            {
                return registered;
            }

            var requested = Normalize(requestedInstallRoot);
            if (!migrationMode && !String.Equals(requested, registered, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Existing installation cannot change path during normal upgrade.");
            }

            return requested;
        }

        return string.IsNullOrWhiteSpace(requestedInstallRoot)
            ? null
            : Normalize(requestedInstallRoot);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
