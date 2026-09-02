$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) ('HuahaiClipboard.Preflight.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'Probe.cs'
$probeExe = Join-Path $fixtureRoot 'Probe.exe'
$csc = Join-Path (Join-Path $env:WINDIR 'Microsoft.NET') 'Framework64\v4.0.30319\csc.exe'
try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;
using System.IO;

internal static class Probe
{
    private static int Main(string[] args)
    {
        var source = Path.Combine(args[0], "source");
        var output = Path.Combine(args[0], "snapshots");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "history.dat"), "history");
        Directory.CreateDirectory(Path.Combine(source, "images"));
        File.WriteAllText(Path.Combine(source, "images", "one.bin"), "image");
        var snapshot = UpgradePreflightPolicy.CreateVerifiedSnapshot(source, output);
        Assert(File.Exists(Path.Combine(snapshot, "history.dat")), "snapshot must include history");
        Assert(File.Exists(Path.Combine(snapshot, "images", "one.bin")), "snapshot must include attachments");
        Assert(File.Exists(Path.Combine(snapshot, "manifest.sha256")), "snapshot must include manifest");
        Assert(UpgradePreflightPolicy.VerifySnapshot(snapshot), "snapshot manifest must verify");
        Console.WriteLine("passed");
        return 0;
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding ASCII
    & $csc /nologo /target:exe /out:$probeExe (Join-Path $projectRoot 'installer\UpgradePreflightPolicy.cs') $probeSource
    if ($LASTEXITCODE -ne 0) { throw 'upgrade preflight probe compilation failed' }
    $output = & $probeExe $fixtureRoot
    if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') { throw "upgrade preflight probe failed: $output" }
    [pscustomobject]@{ Status = 'passed' } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { [IO.Directory]::Delete($fixtureRoot, $true) }
}
