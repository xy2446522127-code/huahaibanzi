$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\InstallSwapTransaction.cs'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboard.InstallSwapTests.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'InstallSwapTransactionProbe.cs'
$probeExe = Join-Path $fixtureRoot 'InstallSwapTransactionProbe.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;
using System.IO;

internal static class InstallSwapTransactionProbe
{
    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        VerifyLockedBackupCleanupCommitsNewVersion(root);
        VerifyActivationFailureRestoresOldVersion(root, "shortcut");
        VerifyActivationFailureRestoresOldVersion(root, "registry");
        VerifyLockedCandidatePreservesBothRunnableVersions(root);
        Console.WriteLine("passed");
        return 0;
    }

    private static void VerifyLockedBackupCleanupCommitsNewVersion(string root)
    {
        string scenario = Path.Combine(root, "locked-backup-cleanup");
        Paths paths = CreateScenario(scenario);
        InstallSwapResult result = InstallSwapTransaction.Execute(
            paths.Staging,
            paths.Install,
            paths.Backup,
            Directory.Exists,
            Directory.Move,
            path => false,
            delegate { });

        Assert(File.Exists(Path.Combine(paths.Install, "new.txt")), "new version must remain active after backup cleanup fails");
        Assert(File.Exists(Path.Combine(paths.Backup, "old.txt")), "complete old backup must remain after cleanup fails");
        Assert(result.BackupCleanupPending, "locked backup cleanup must be reported explicitly");
    }

    private static void VerifyActivationFailureRestoresOldVersion(string root, string failureStep)
    {
        string scenario = Path.Combine(root, failureStep + "-failure");
        Paths paths = CreateScenario(scenario);
        Exception failure = null;
        try
        {
            InstallSwapTransaction.Execute(
                paths.Staging,
                paths.Install,
                paths.Backup,
                Directory.Exists,
                Directory.Move,
                DeleteDirectory,
                delegate { throw new InvalidOperationException(failureStep + " failed"); });
        }
        catch (InvalidOperationException error)
        {
            failure = error;
        }

        Assert(failure != null && failure.Message.Contains(failureStep), failureStep + " failure must remain visible");
        Assert(File.Exists(Path.Combine(paths.Install, "old.txt")), failureStep + " failure must restore the complete old version");
        Assert(!Directory.Exists(paths.Backup), failureStep + " failure must consume the restored backup");
        Assert(!File.Exists(Path.Combine(paths.Install, "new.txt")), failureStep + " failure must remove the candidate version");
    }

    private static void VerifyLockedCandidatePreservesBothRunnableVersions(string root)
    {
        string scenario = Path.Combine(root, "locked-candidate");
        Paths paths = CreateScenario(scenario);
        Exception failure = null;
        try
        {
            InstallSwapTransaction.Execute(
                paths.Staging,
                paths.Install,
                paths.Backup,
                Directory.Exists,
                Directory.Move,
                path => false,
                delegate { throw new InvalidOperationException("shortcut failed"); });
        }
        catch (InvalidOperationException error)
        {
            failure = error;
        }

        Assert(failure != null && failure.Message.Contains("rollback could not remove the candidate"), "locked candidate rollback must fail explicitly");
        Assert(File.Exists(Path.Combine(paths.Install, "new.txt")), "locked candidate must remain runnable");
        Assert(File.Exists(Path.Combine(paths.Backup, "old.txt")), "old version backup must remain complete when candidate cannot be removed");
    }

    private static Paths CreateScenario(string root)
    {
        string staging = Path.Combine(root, "staging");
        string install = Path.Combine(root, "install");
        string backup = Path.Combine(root, "backup");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(staging, "new.txt"), "new version");
        File.WriteAllText(Path.Combine(install, "old.txt"), "old version");
        return new Paths(staging, install, backup);
    }

    private static bool DeleteDirectory(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, true);
        return !Directory.Exists(path);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Paths
    {
        public Paths(string staging, string install, string backup)
        {
            Staging = staging;
            Install = install;
            Backup = backup;
        }

        public string Staging { get; private set; }
        public string Install { get; private set; }
        public string Backup { get; private set; }
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding UTF8

    & $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
    if ($LASTEXITCODE -ne 0) { throw "Install swap transaction probe compilation failed with exit code $LASTEXITCODE" }

    $output = & $probeExe $fixtureRoot
    if ($LASTEXITCODE -ne 0 -or $output -ne 'passed') {
        throw "Install swap transaction probe failed: $output"
    }

    [pscustomobject]@{ Status = 'passed'; Scenarios = 4 } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [System.IO.Path]::GetFullPath($fixtureRoot)
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^HuahaiClipboard\.InstallSwapTests\.[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected install swap fixture path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
