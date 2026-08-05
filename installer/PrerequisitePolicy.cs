using System;
using System.Collections.Generic;

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

    // 0 为成功，1638 为同版本已安装，3010 为成功但建议重启。
    internal static bool IsAcceptedInstallerExitCode(int exitCode)
    {
        return exitCode == 0 || exitCode == 1638 || exitCode == 3010;
    }
}
