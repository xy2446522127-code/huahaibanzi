param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9233
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$webProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.ScaleSmoke.' + [guid]::NewGuid().ToString('N'))
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$previousUserKey = $env:HUAHAI_CLIPBOARD_USER_KEY

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiScaleWindowProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);
}
'@

function Get-WindowSize([IntPtr]$handle) {
    $rect = [HuahaiScaleWindowProbe+Rect]::new()
    if (-not [HuahaiScaleWindowProbe]::GetWindowRect($handle, [ref]$rect)) {
        throw 'The native panel rectangle could not be read.'
    }
    return [pscustomobject]@{ Width = $rect.Right - $rect.Left; Height = $rect.Bottom - $rect.Top }
}

$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
$env:HUAHAI_CLIPBOARD_USER_KEY = 'scale-smoke-user'
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $handle = $process.MainWindowHandle }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            if ($handle -ne [IntPtr]::Zero) { break }
        } catch { }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'Installed app did not expose a top-level window.' }

    $homeResult = node $webProbe $DebugPort settings-home | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $homeResult.returned -or $homeResult.hash -ne '#panel') {
        throw 'The settings fox did not return from a nested settings page to the main panel.'
    }

    $before = Get-WindowSize $handle
    $observed = @()
    foreach ($percent in @(81, 83, 117, 149, 159)) {
        $ratio = $percent / 100
        $scale = node $webProbe $DebugPort set-scale $ratio | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $scale.scaled -or $scale.label -ne "$percent%") {
            throw "The production scale bridge did not commit $percent percent."
        }
        $expectedWidth = [Math]::Round(430 * $ratio)
        $expectedHeight = [Math]::Round(680 * $ratio)
        $after = Get-WindowSize $handle
        if ([Math]::Abs($after.Width - $expectedWidth) -gt 2 -or [Math]::Abs($after.Height - $expectedHeight) -gt 2) {
            throw "Panel scaling reached the wrong native size at $percent percent: $($after.Width)x$($after.Height)"
        }
        $observed += "$percent%=$($after.Width)x$($after.Height)"
    }

    $scrub = node $webProbe $DebugPort scrub-scale '159,81,149,83,117,159' | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $scrub.scrubbed -or $scrub.blankSamples -ne 0 -or $scrub.finalLabel -ne '159%') {
        throw "Rapid reversal scale scrub lost render continuity: $($scrub | ConvertTo-Json -Compress)"
    }

    $settingsPath = Join-Path $testRoot 'Data\scale-smoke-user\settings.json'
    for ($attempt = 0; $attempt -lt 40 -and -not (Test-Path -LiteralPath $settingsPath); $attempt++) {
        Start-Sleep -Milliseconds 100
    }
    $saved = Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    if ([Math]::Abs([double]$saved.Appearance.PanelScale - 1.59) -gt 0.0001) {
        throw "The final committed scale was not persisted exactly once at the settled value: $($saved.Appearance.PanelScale)"
    }

    [pscustomobject]@{
        Status = 'passed'
        Before = "$($before.Width)x$($before.Height)"
        SettingsHome = $homeResult.returned
        Samples = $observed
        RapidReversalSamples = $scrub.samples.Count
        BlankSamples = $scrub.blankSamples
        FinalScale = $saved.Appearance.PanelScale
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $env:HUAHAI_CLIPBOARD_USER_KEY = $previousUserKey
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedTestRoot -Leaf).StartsWith('HuahaiClipboard.ScaleSmoke.')) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
