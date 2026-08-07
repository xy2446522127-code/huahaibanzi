param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9254
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$webProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.PointerSmoke.' + [guid]::NewGuid().ToString('N'))
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiPointerInteractionProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

function Get-WindowRectValue([IntPtr]$Handle) {
    $rect = [HuahaiPointerInteractionProbe+Rect]::new()
    if (-not [HuahaiPointerInteractionProbe]::GetWindowRect($Handle, [ref]$rect)) {
        throw 'The native panel rectangle could not be read.'
    }

    [pscustomobject]@{
        Left = $rect.Left
        Top = $rect.Top
        Width = $rect.Right - $rect.Left
        Height = $rect.Bottom - $rect.Top
        Right = $rect.Right
        Bottom = $rect.Bottom
    }
}

function Invoke-LeftDrag([int]$FromX, [int]$FromY, [int]$ToX, [int]$ToY, [IntPtr]$TrackHandle = [IntPtr]::Zero) {
    $move = 0x0001
    $leftDown = 0x0002
    $leftUp = 0x0004
    [HuahaiPointerInteractionProbe]::SetCursorPos($FromX, $FromY) | Out-Null
    Start-Sleep -Milliseconds 120
    [HuahaiPointerInteractionProbe]::mouse_event($leftDown, 0, 0, 0, [UIntPtr]::Zero)
    $samples = @()
    $watch = [Diagnostics.Stopwatch]::StartNew()
    for ($step = 1; $step -le 30; $step++) {
        $x = [Math]::Round($FromX + (($ToX - $FromX) * $step / 30))
        $y = [Math]::Round($FromY + (($ToY - $FromY) * $step / 30))
        [HuahaiPointerInteractionProbe]::SetCursorPos($x, $y) | Out-Null
        [HuahaiPointerInteractionProbe]::mouse_event($move, 0, 0, 0, [UIntPtr]::Zero)
        Start-Sleep -Milliseconds 12
        if ($TrackHandle -ne [IntPtr]::Zero) {
            $rect = Get-WindowRectValue $TrackHandle
            $samples += [pscustomobject]@{ TimeMs = $watch.ElapsedMilliseconds; Left = $rect.Left; Top = $rect.Top }
        }
    }
    [HuahaiPointerInteractionProbe]::mouse_event($leftUp, 0, 0, 0, [UIntPtr]::Zero)
    $samples
}

$originalCursor = [HuahaiPointerInteractionProbe+Point]::new()
[HuahaiPointerInteractionProbe]::GetCursorPos([ref]$originalCursor) | Out-Null
$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    $handle = [IntPtr]::Zero
    $webViewReady = $false
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) { $handle = $process.MainWindowHandle }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            $webViewReady = $true
            break
        } catch { }
    }
    if (-not $webViewReady) { throw 'Installed app did not expose a ready WebView runtime.' }

    $summon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    if (-not $summon.WaitForExit(10000) -or $summon.ExitCode -ne 0) {
        throw 'The background panel could not be summoned for pointer interaction testing.'
    }
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $handle = $process.MainWindowHandle
            break
        }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'Summoning did not expose the WebView window handle.' }

    [HuahaiPointerInteractionProbe]::BringWindowToTop($handle) | Out-Null
    [HuahaiPointerInteractionProbe]::SetForegroundWindow($handle) | Out-Null
    Start-Sleep -Milliseconds 750
    $before = Get-WindowRectValue $handle
    $hitTest = node $webProbe $DebugPort hit-test '105,34' | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'The panel drag hit-test probe failed.' }
    $pointerAudit = node $webProbe $DebugPort arm-pointer-log | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $pointerAudit.armed) { throw 'The pointer event audit could not be armed.' }
    $dragPoint = [HuahaiPointerInteractionProbe+Point]::new()
    $dragPoint.X = $before.Left + 105
    $dragPoint.Y = $before.Top + 34
    $dragTarget = [HuahaiPointerInteractionProbe]::WindowFromPoint($dragPoint)
    $dragTargetProcessId = [uint32]0
    [HuahaiPointerInteractionProbe]::GetWindowThreadProcessId($dragTarget, [ref]$dragTargetProcessId) | Out-Null
    if ($dragTargetProcessId -ne [uint32]$process.Id) {
        throw "The panel drag point is covered by another process. TargetPid=$dragTargetProcessId TestPid=$($process.Id)"
    }
    $moveSamples = @(Invoke-LeftDrag ($before.Left + 105) ($before.Top + 34) ($before.Left - 15) ($before.Top - 26) $handle)
    Start-Sleep -Milliseconds 700
    $afterMove = Get-WindowRectValue $handle
    if ([Math]::Abs($afterMove.Left - $before.Left) -lt 80 -or [Math]::Abs($afterMove.Top - $before.Top) -lt 35) {
        $placementFile = Join-Path $testRoot 'HuahaiClipboard\window-positions.json'
        $pointerLog = node $webProbe $DebugPort read-pointer-log | ConvertFrom-Json
        $bridgeStatus = node $webProbe $DebugPort read-status | ConvertFrom-Json
        throw "Dragging the visible panel header did not move the native window. Before=$($before.Left),$($before.Top) After=$($afterMove.Left),$($afterMove.Top) BridgeSavedPlacement=$(Test-Path -LiteralPath $placementFile) BridgeStatus=$($bridgeStatus | ConvertTo-Json -Compress) HitTag=$($hitTest.tag) HitId=$($hitTest.id) HitClasses=$($hitTest.classes) HitInteractive=$($hitTest.interactive) PointerEvents=$($pointerLog.events | ConvertTo-Json -Compress)"
    }

    $uniquePositions = @($moveSamples | ForEach-Object { "$($_.Left),$($_.Top)" } | Select-Object -Unique).Count
    $longestStall = 0
    $currentStall = 0
    $previousPosition = $null
    foreach ($sample in $moveSamples) {
        $position = "$($sample.Left),$($sample.Top)"
        if ($position -eq $previousPosition) { $currentStall++ } else { $currentStall = 0 }
        $longestStall = [Math]::Max($longestStall, $currentStall)
        $previousPosition = $position
    }
    if ($uniquePositions -lt 15 -or $longestStall -gt 4) {
        throw "Panel dragging was not visually continuous. UniquePositions=$uniquePositions/30 LongestStallSamples=$longestStall Track=$($moveSamples | ConvertTo-Json -Compress)"
    }

    $resizeGrab = node $webProbe $DebugPort resize-grab-point | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'The visible resize-grip point could not be resolved.' }
    $resizeX = $afterMove.Left + [Math]::Round($afterMove.Width * $resizeGrab.xRatio)
    $resizeY = $afterMove.Top + [Math]::Round($afterMove.Height * $resizeGrab.yRatio)
    $resizePoint = [HuahaiPointerInteractionProbe+Point]::new()
    $resizePoint.X = $resizeX
    $resizePoint.Y = $resizeY
    $resizeTarget = [HuahaiPointerInteractionProbe]::WindowFromPoint($resizePoint)
    $resizeTargetProcessId = [uint32]0
    [HuahaiPointerInteractionProbe]::GetWindowThreadProcessId($resizeTarget, [ref]$resizeTargetProcessId) | Out-Null
    $null = Invoke-LeftDrag $resizeX $resizeY ($resizeX + 92) ($resizeY + 92)
    $afterScale = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 100
        $afterScale = Get-WindowRectValue $handle
        if ($afterScale.Width -ge ($afterMove.Width + 40) -and
            $afterScale.Height -ge ($afterMove.Height + 60)) { break }
    }
    if ($afterScale.Width -lt ($afterMove.Width + 40) -or
        $afterScale.Height -lt ($afterMove.Height + 60)) {
        $resizePointerLog = node $webProbe $DebugPort read-pointer-log | ConvertFrom-Json
        throw "Dragging the visible lower-right resize grip did not enlarge the native window. Before=$($afterMove.Width)x$($afterMove.Height) After=$($afterScale.Width)x$($afterScale.Height) Grab=$resizeX,$resizeY Ratios=$($resizeGrab.xRatio),$($resizeGrab.yRatio) TargetPid=$resizeTargetProcessId TestPid=$($process.Id) PseudoRight=$($resizeGrab.pseudoRight) PseudoBottom=$($resizeGrab.pseudoBottom) PointerEvents=$($resizePointerLog.events | ConvertTo-Json -Compress)"
    }

    $widthScale = $afterScale.Width / 430.0
    $heightScale = $afterScale.Height / 680.0
    if ([Math]::Abs($widthScale - $heightScale) -gt 0.02) {
        throw "Pointer resizing changed the panel aspect ratio. WidthScale=$widthScale HeightScale=$heightScale"
    }

    [pscustomobject]@{
        Status = 'passed'
        PositionBefore = "$($before.Left),$($before.Top)"
        PositionAfter = "$($afterMove.Left),$($afterMove.Top)"
        DragUniquePositions = $uniquePositions
        DragLongestStallSamples = $longestStall
        SizeBefore = "$($afterMove.Width)x$($afterMove.Height)"
        SizeAfter = "$($afterScale.Width)x$($afterScale.Height)"
    } | ConvertTo-Json -Compress
}
finally {
    [HuahaiPointerInteractionProbe]::SetCursorPos($originalCursor.X, $originalCursor.Y) | Out-Null
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedTestRoot -Leaf).StartsWith('HuahaiClipboard.PointerSmoke.')) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
