param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9224
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$clickProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewHideButtonSmoke.cjs'

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiWindowProbe {
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
}
'@

# 仅向本次启动的 WebView2 子进程开放本机 CDP 端口。
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $handle = $process.MainWindowHandle }

        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            if ($handle -ne [IntPtr]::Zero) { break }
        } catch { }
    }

    if ($handle -eq [IntPtr]::Zero) { throw 'Installed app did not expose a top-level window.' }
    $visibleBefore = [HuahaiWindowProbe]::IsWindowVisible($handle)
    $clickResult = node $clickProbe $DebugPort hide-with-background-disabled
    if ($LASTEXITCODE -ne 0) { throw 'The WebView minimize button probe failed.' }

    Start-Sleep -Milliseconds 700
    $process.Refresh()
    $visibleAfter = [HuahaiWindowProbe]::IsWindowVisible($handle)
    if (-not $visibleBefore -or $visibleAfter -or $process.HasExited) {
        throw 'The minimize button must hide the window while keeping the process alive.'
    }

    # 隐藏后 WebView2 会挂起；先通过第二实例唤出并恢复内容，再还原用户偏好。
    $summon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    if (-not $summon.WaitForExit(15000) -or $summon.ExitCode -ne 0) {
        throw 'The hidden panel could not be summoned before restoring its background setting.'
    }
    $visibleAfterSummon = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 100
        if ([HuahaiWindowProbe]::IsWindowVisible($handle)) {
            $visibleAfterSummon = $true
            break
        }
    }
    if (-not $visibleAfterSummon) {
        throw 'The hidden panel did not resume before restoring its background setting.'
    }
    $restoreResult = node $clickProbe $DebugPort restore-background
    if ($LASTEXITCODE -ne 0) { throw 'The background setting restore probe failed.' }

    [pscustomobject]@{
        Status = 'passed'
        Click = $clickResult
        Restore = $restoreResult
        ProcessAlive = -not $process.HasExited
        VisibleBefore = $visibleBefore
        VisibleAfter = $visibleAfter
        VisibleAfterSummon = $visibleAfterSummon
        ProcessId = $process.Id
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
}
