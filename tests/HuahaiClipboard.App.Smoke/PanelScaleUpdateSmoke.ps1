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

    $before = Get-WindowSize $handle
    $scale = node $webProbe $DebugPort set-scale 1.2 | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $scale.scaled) { throw 'The WebView scale control did not reach the production bridge.' }

    $after = $null
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 100
        $after = Get-WindowSize $handle
        if ([Math]::Abs($after.Width - 516) -le 2 -and [Math]::Abs($after.Height - 816) -le 2) { break }
    }
    if ([Math]::Abs($after.Width - 516) -gt 2 -or [Math]::Abs($after.Height - 816) -gt 2) {
        throw "Panel scaling did not resize the native window proportionally. Before=$($before.Width)x$($before.Height) After=$($after.Width)x$($after.Height)"
    }

    $update = node $webProbe $DebugPort check-update | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $update.completed -or $update.statusClass -notmatch '(available|current|error)') {
        throw "The production update bridge did not return a terminal result: $($update | ConvertTo-Json -Compress)"
    }

    [pscustomobject]@{
        Status = 'passed'
        Before = "$($before.Width)x$($before.Height)"
        After = "$($after.Width)x$($after.Height)"
        ScaleLabel = $scale.label
        UpdateStatus = $update.statusClass
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedTestRoot -Leaf).StartsWith('HuahaiClipboard.ScaleSmoke.')) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
