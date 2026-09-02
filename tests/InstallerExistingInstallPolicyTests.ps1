$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) ('HuahaiClipboard.ExistingInstallPolicy.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'Probe.cs'
$probeExe = Join-Path $fixtureRoot 'Probe.exe'
$csc = Join-Path (Join-Path $env:WINDIR 'Microsoft.NET') 'Framework64\v4.0.30319\csc.exe'
try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;

internal static class Probe
{
    private static int Main()
    {
        Assert(BootstrapperInstallPathPolicy.Resolve(null, @"F:\Existing\HuahaiClipboard", false) == @"F:\Existing\HuahaiClipboard", "existing install must be reused");
        Assert(BootstrapperInstallPathPolicy.Resolve(null, null, false) == null, "new install may use picker");
        Throws(() => BootstrapperInstallPathPolicy.Resolve(@"G:\Other\HuahaiClipboard", @"F:\Existing\HuahaiClipboard", false), "changing path must be rejected");
        Assert(BootstrapperInstallPathPolicy.Resolve(@"G:\Other\HuahaiClipboard", @"F:\Existing\HuahaiClipboard", true) == @"G:\Other\HuahaiClipboard", "explicit migration mode may change path");
        Console.WriteLine("passed");
        return 0;
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Throws(Action action, string message) { try { action(); } catch (InvalidOperationException) { return; } throw new InvalidOperationException(message); }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding ASCII
    & $csc /nologo /target:exe /out:$probeExe (Join-Path $projectRoot 'installer\BootstrapperInstallPathPolicy.cs') $probeSource
    if ($LASTEXITCODE -ne 0) { throw 'policy probe compilation failed' }
    $output = & $probeExe
    if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') { throw "policy probe failed: $output" }
    [pscustomobject]@{ Status = 'passed' } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { [IO.Directory]::Delete($fixtureRoot, $true) }
}
