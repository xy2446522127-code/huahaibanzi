$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\InstallTargetPolicy.cs'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboard.InstallTargetTests.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'InstallTargetPolicyProbe.cs'
$probeExe = Join-Path $fixtureRoot 'InstallTargetPolicyProbe.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;
using System.IO;

internal static class InstallTargetPolicyProbe
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string dataRoot = Path.Combine(root, "LocalAppData", "HuahaiClipboard");

        string absent = Path.Combine(root, "Absent", "HuahaiClipboard");
        InstallTargetPolicy.Validate(absent, null, dataRoot);

        string empty = Path.Combine(root, "Empty", "HuahaiClipboard");
        Directory.CreateDirectory(empty);
        InstallTargetPolicy.Validate(empty, null, dataRoot);

        string foreign = Path.Combine(root, "Foreign", "HuahaiClipboard");
        Directory.CreateDirectory(foreign);
        string personalFile = Path.Combine(foreign, "personal.txt");
        File.WriteAllText(personalFile, "keep me");
        Throws(() => InstallTargetPolicy.Validate(foreign, null, dataRoot), "unregistered non-empty target must be rejected");
        Assert(File.ReadAllText(personalFile) == "keep me", "rejected target must remain byte-identical");

        string legacy = Path.Combine(root, "Legacy", "HuahaiClipboard");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "HuahaiClipboard.exe"), "app");
        File.WriteAllText(Path.Combine(legacy, "HuahaiClipboard.App.dll"), "managed app");
        File.WriteAllText(Path.Combine(legacy, "Uninstall.ps1"), "uninstall");
        InstallTargetPolicy.Validate(legacy, legacy, dataRoot);

        Throws(
            () => InstallTargetPolicy.Validate(legacy, Path.Combine(root, "Other", "HuahaiClipboard"), dataRoot),
            "registered location mismatch must be rejected");

        string overlap = Path.Combine(dataRoot, "HuahaiClipboard");
        Throws(() => InstallTargetPolicy.Validate(overlap, null, dataRoot), "data-directory overlap must be rejected");

        string wrongLeaf = Path.Combine(root, "HuahaiClipboard-Copy");
        Throws(() => InstallTargetPolicy.Validate(wrongLeaf, null, dataRoot), "unsafe CLI target name must be rejected");

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
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding ASCII

    & $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
    if ($LASTEXITCODE -ne 0) { throw "Install target policy probe compilation failed with exit code $LASTEXITCODE" }

    $output = & $probeExe $fixtureRoot
    if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') {
        throw "Install target policy probe failed: $output"
    }

    [pscustomobject]@{ Status = 'passed'; Fixture = $fixtureRoot } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [System.IO.Path]::GetFullPath($fixtureRoot)
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^HuahaiClipboard\.InstallTargetTests\.[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected install target test fixture: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
