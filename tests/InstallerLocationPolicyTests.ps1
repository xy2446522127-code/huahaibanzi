$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\InstallLocationPolicy.cs'
$probeRoot = Join-Path $projectRoot 'dist\install-location-policy-probe'
$probeSource = Join-Path $probeRoot 'InstallLocationPolicyProbe.cs'
$probeExe = Join-Path $probeRoot 'InstallLocationPolicyProbe.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $policySource)) { throw "Missing install location policy: $policySource" }
New-Item -ItemType Directory -Path $probeRoot -Force | Out-Null

@'
using System;

internal static class InstallLocationPolicyProbe
{
    private static int Main()
    {
        Assert(InstallLocationPolicy.DefaultForRoots(new[] { @"C:\", @"F:\", @"D:\" }, "HuahaiClipboard") == @"F:\HuahaiClipboard", "default must use the first non-C drive");
        Assert(InstallLocationPolicy.Resolve(@"F:\HuahaiClipboard\", @"F:\HuahaiClipboard") == @"F:\HuahaiClipboard", "custom F drive path must normalize");
        Throws(() => InstallLocationPolicy.Resolve(null, @"C:\Users\Fixture\AppData\Local\Programs\HuahaiClipboard"), "C drive default must be rejected");
        Throws(() => InstallLocationPolicy.Resolve(@"C:\Tools\HuahaiClipboard", @"F:\HuahaiClipboard"), "custom C drive path must be rejected");
        Throws(() => InstallLocationPolicy.Resolve(@"F:\", @"F:\HuahaiClipboard"), "drive root must be rejected");
        ThrowsInvalidOperation(() => InstallLocationPolicy.DefaultForRoots(new[] { @"C:\" }, "HuahaiClipboard"), "install must stop when no non-C drive is available");
        Console.WriteLine("passed");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void ThrowsInvalidOperation(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding ASCII

& $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
if ($LASTEXITCODE -ne 0) { throw "Install location policy probe compilation failed with exit code $LASTEXITCODE" }
$output = & $probeExe
if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') { throw "Install location policy probe failed: $output" }

[pscustomobject]@{ Status = 'passed'; Probe = $probeExe } | ConvertTo-Json -Compress
