$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\InstallDataPreserver.cs'
$fixtureRoot = Join-Path ([IO.Path]::GetFullPath([IO.Path]::GetTempPath())) ('HuahaiClipboard.InstallData.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'InstallDataProbe.cs'
$probeExe = Join-Path $fixtureRoot 'InstallDataProbe.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;
using System.IO;

internal static class InstallDataProbe
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string install = Path.Combine(root, "installed", "HuahaiClipboard");
        string staging = Path.Combine(root, "staging");
        string sourceData = Path.Combine(install, "Data", "S-1-5-21-1000");
        Directory.CreateDirectory(sourceData);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(sourceData, "settings.json"), "settings-v1");
        File.WriteAllText(Path.Combine(sourceData, "history.dat"), "history-v1");

        InstallDataPreserver.CopyIntoCandidate(install, staging);

        string candidateData = Path.Combine(staging, "Data", "S-1-5-21-1000");
        Assert(File.ReadAllText(Path.Combine(candidateData, "settings.json")) == "settings-v1", "settings must survive update staging");
        Assert(File.ReadAllText(Path.Combine(candidateData, "history.dat")) == "history-v1", "history must survive update staging");
        Assert(File.ReadAllText(Path.Combine(sourceData, "settings.json")) == "settings-v1", "preservation must not mutate active data before commit");

        Console.WriteLine("passed");
        return 0;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding UTF8

    & $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
    if ($LASTEXITCODE -ne 0) { throw "Install data probe compilation failed with exit code $LASTEXITCODE" }
    $output = & $probeExe $fixtureRoot
    if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') { throw "Install data probe failed: $output" }
    [pscustomobject]@{ Status = 'passed'; Scenario = 'update-preserves-install-root-data' } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [IO.Path]::GetFullPath($fixtureRoot)
        if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^HuahaiClipboard\.InstallData\.[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected install data fixture path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
