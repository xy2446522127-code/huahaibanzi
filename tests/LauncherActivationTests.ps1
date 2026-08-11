$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$launcherSource = Join-Path $root 'launcher\HuahaiClipboard.Launcher.cpp'
$launcherBuild = Join-Path $root 'launcher\Build-Launcher.ps1'
if (-not (Test-Path -LiteralPath $launcherSource -PathType Leaf)) {
    throw "Missing launcher source: $launcherSource"
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.LauncherTests.' + [guid]::NewGuid().ToString('N'))
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$eventName = 'Local\HuahaiClipboard.LauncherTests.' + [guid]::NewGuid().ToString('N')
$marker = Join-Path $fixtureRoot 'app-launched.txt'
$launcherExe = Join-Path $fixtureRoot 'HuahaiClipboard.Launcher.exe'
$appExe = Join-Path $fixtureRoot 'HuahaiClipboard.App.exe'
$testLauncherSource = Join-Path $fixtureRoot 'Launcher.cpp'
$dummySource = Join-Path $fixtureRoot 'DummyApp.cs'

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    $launcher = Get-Content -Raw -Encoding UTF8 -LiteralPath $launcherSource
    $launcher = $launcher.Replace(
        'Local\\HuahaiClipboard.Activate.v1',
        $eventName.Replace('\', '\\'))
    [IO.File]::WriteAllText($testLauncherSource, $launcher, [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($dummySource, @'
using System;
using System.IO;
internal static class DummyApp
{
    private static void Main()
    {
        File.WriteAllText(Environment.GetEnvironmentVariable("HUAHAI_LAUNCHER_TEST_MARKER"), "launched");
    }
}
'@, [Text.UTF8Encoding]::new($false))

    & $launcherBuild -OutputPath $launcherExe -SourcePath $testLauncherSource | Out-Null
    & $csc /nologo /target:winexe /optimize+ "/out:$appExe" $dummySource
    if ($LASTEXITCODE -ne 0) { throw 'Dummy app compilation failed.' }

    $signal = [Threading.EventWaitHandle]::new($false, [Threading.EventResetMode]::AutoReset, $eventName)
    try {
        $watch = [Diagnostics.Stopwatch]::StartNew()
        $forwarderInfo = [Diagnostics.ProcessStartInfo]::new($launcherExe)
        $forwarderInfo.UseShellExecute = $false
        $forwarderInfo.CreateNoWindow = $true
        $forwarder = [Diagnostics.Process]::Start($forwarderInfo)
        if (-not $forwarder.WaitForExit(2000)) { throw 'Launcher did not exit after forwarding activation.' }
        if (-not $signal.WaitOne(2000)) { throw 'Launcher did not signal the running application.' }
        if ($watch.ElapsedMilliseconds -ge 250) {
            throw "Launcher forwarding exceeded 250 ms: $($watch.ElapsedMilliseconds) ms."
        }
        if (Test-Path -LiteralPath $marker) { throw 'Launcher started a duplicate app while the signal existed.' }
    }
    finally {
        $signal.Dispose()
    }

    $env:HUAHAI_LAUNCHER_TEST_MARKER = $marker
    $starterInfo = [Diagnostics.ProcessStartInfo]::new($launcherExe)
    $starterInfo.UseShellExecute = $false
    $starterInfo.CreateNoWindow = $true
    $starter = [Diagnostics.Process]::Start($starterInfo)
    if (-not $starter.WaitForExit(2000)) { throw 'Launcher did not exit after starting the application.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(2)
    while (-not (Test-Path -LiteralPath $marker) -and [DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 20
    }
    if (-not (Test-Path -LiteralPath $marker)) { throw 'Launcher did not start the application when no signal existed.' }

    [pscustomobject]@{ Status = 'passed'; ForwardingLimitMs = 250 } | ConvertTo-Json -Compress
}
finally {
    Remove-Item Env:\HUAHAI_LAUNCHER_TEST_MARKER -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
