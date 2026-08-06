$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\PrerequisitePolicy.cs'
$probeRoot = Join-Path $projectRoot 'dist\prerequisite-policy-probe'
$probeSource = Join-Path $probeRoot 'PolicyProbe.cs'
$probeExe = Join-Path $probeRoot 'PolicyProbe.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null
@'
using System;

internal static class PolicyProbe
{
    private static int Main()
    {
        Assert(PrerequisitePolicy.NeedsDotNetDesktopRuntime(new string[0]), "missing .NET runtime must install");
        Assert(PrerequisitePolicy.NeedsDotNetDesktopRuntime(new[] { "7.0.20", "9.0.1" }), "non-8 runtimes must install");
        Assert(!PrerequisitePolicy.NeedsDotNetDesktopRuntime(new[] { "8.0.0", "9.0.1" }), ".NET 8 runtime must skip");
        Assert(PrerequisitePolicy.NeedsWindowsAppRuntime(new string[0]), "missing Windows App Runtime must install");
        Assert(PrerequisitePolicy.NeedsWindowsAppRuntime(new[] { "not-a-version" }), "an invalid Windows App Runtime query result must install");
        Assert(!PrerequisitePolicy.NeedsWindowsAppRuntime(new[] { "7000.785.2325.0" }), "the exact Windows App Runtime 1.7 x64 package version returned by Appx must skip");
        Assert(PrerequisitePolicy.NeedsWebView2Runtime(new string[0]), "missing WebView2 runtime must install");
        Assert(PrerequisitePolicy.NeedsWebView2Runtime(new[] { "108.0.0.0" }), "obsolete WebView2 runtime must install");
        Assert(!PrerequisitePolicy.NeedsWebView2Runtime(new[] { "109.0.1518.0", "140.0.0.0" }), "supported WebView2 runtime must skip");
        Assert(PrerequisitePolicy.IsAcceptedInstallerExitCode(0), "exit 0 must pass");
        Assert(PrerequisitePolicy.IsAcceptedInstallerExitCode(1638), "already installed must pass");
        Assert(PrerequisitePolicy.IsAcceptedInstallerExitCode(3010), "restart required must pass");
        Assert(!PrerequisitePolicy.IsAcceptedInstallerExitCode(1602), "user cancellation must fail");
        Console.WriteLine("passed");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding ASCII

& $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
if ($LASTEXITCODE -ne 0) { throw "Policy probe compilation failed with exit code $LASTEXITCODE" }

$output = & $probeExe
if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') {
    throw "Policy probe failed: $output"
}

[pscustomobject]@{ Status = 'passed'; Probe = $probeExe } | ConvertTo-Json -Compress
