using System;
using System.Collections.Generic;

internal enum PrerequisiteInstallOutcome
{
    Failed,
    Succeeded,
    RestartRequired
}

internal static class PrerequisitePolicy
{
    // 只要存在任意 .NET 8 Windows Desktop Runtime 即满足应用运行要求。
    internal static bool NeedsDotNetDesktopRuntime(IEnumerable<string> installedVersions)
    {
        if (installedVersions == null)
            return true;

        foreach (string value in installedVersions)
        {
            Version version;
            if (Version.TryParse(value, out version) && version.Major == 8)
                return false;
        }

        return true;
    }

    internal static bool NeedsWindowsAppRuntime(IEnumerable<string> installedVersions)
    {
        if (installedVersions == null)
            return true;

        foreach (string value in installedVersions)
        {
            Version version;
            // Bootstrapper 已按精确的 Microsoft.WindowsAppRuntime.1.7 包名和 x64/Neutral
            // 架构过滤；这里接收 Appx 返回的 7000.* MSIX 版本，而不是营销版本 1.7。
            if (Version.TryParse(value, out version))
                return false;
        }

        return true;
    }

    internal static bool NeedsWebView2Runtime(IEnumerable<string> installedVersions)
    {
        if (installedVersions == null)
            return true;

        foreach (string value in installedVersions)
        {
            Version version;
            if (Version.TryParse(value, out version) && version.Major >= 109)
                return false;
        }

        return true;
    }

    internal static PrerequisiteInstallOutcome ClassifyInstallerExitCode(int exitCode)
    {
        if (exitCode == 3010)
            return PrerequisiteInstallOutcome.RestartRequired;
        if (exitCode == 0 || exitCode == 1638)
            return PrerequisiteInstallOutcome.Succeeded;
        return PrerequisiteInstallOutcome.Failed;
    }

    internal static bool HasMissingRuntime(bool needsDotNet, bool needsWebView2)
    {
        return needsDotNet || needsWebView2;
    }
}
