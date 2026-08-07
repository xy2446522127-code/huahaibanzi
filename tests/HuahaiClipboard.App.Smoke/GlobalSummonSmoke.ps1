param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [ValidateSet('RightDoubleClick', 'CustomKeyboard')][string]$Mode
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.GlobalSummon.' + [guid]::NewGuid().ToString('N'))
$dataRoot = Join-Path $testRoot 'HuahaiClipboard'
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$process = $null

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiGlobalSummonProbe {
    public delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr windowHandle, uint message, IntPtr wParam, IntPtr lParam);

    public static IntPtr FindWindowForProcess(uint expectedProcessId) {
        IntPtr found = IntPtr.Zero;
        EnumWindows((windowHandle, parameter) => {
            uint processId;
            GetWindowThreadProcessId(windowHandle, out processId);
            if (processId != expectedProcessId) return true;
            found = windowHandle;
            return false;
        }, IntPtr.Zero);
        return found;
    }
}
'@

function Invoke-RightDoubleClick {
    $rightDown = [uint32]0x0008
    $rightUp = [uint32]0x0010
    [HuahaiGlobalSummonProbe]::mouse_event($rightDown, 0, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::mouse_event($rightUp, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
    [HuahaiGlobalSummonProbe]::mouse_event($rightDown, 0, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::mouse_event($rightUp, 0, 0, 0, [UIntPtr]::Zero)
}

function Invoke-CustomKeyboard {
    $keyUp = [uint32]0x0002
    [HuahaiGlobalSummonProbe]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x12, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x10, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x87, 0, 0, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x87, 0, $keyUp, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x10, 0, $keyUp, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x12, 0, $keyUp, [UIntPtr]::Zero)
    [HuahaiGlobalSummonProbe]::keybd_event(0x11, 0, $keyUp, [UIntPtr]::Zero)
}

$originalCursor = [HuahaiGlobalSummonProbe+Point]::new()
$null = [HuahaiGlobalSummonProbe]::GetCursorPos([ref]$originalCursor)

try {
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    if ($Mode -eq 'CustomKeyboard') {
        $settings = @{
            Appearance = @{ ThemeId = 'rose-purple'; Opacity = 0.74; BlurAmount = 32; ReflectionStrength = 0.72; CompactMode = $false; PanelScale = 1 }
            Motion = @{ PetalLevel = 1; ReduceMotion = $false; ClickDurationMs = 620; ReducedClickDurationMs = 120 }
            Input = @{ RightDoubleClickEnabled = $true; HotkeyEnabled = $true; ExcludedApplications = @(); CustomShortcut = 'Ctrl+Alt+Shift+F24' }
            Behavior = @{ BackgroundEnabled = $true; AutoCleanupDays = 7; CheckUpdatesOnStartup = $false }
        }
        $settings | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath (Join-Path $dataRoot 'settings.json') -Encoding utf8
    }

    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
    $process = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -WorkingDirectory (Split-Path $resolvedExe) -PassThru
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) { throw "Background app exited with code $($process.ExitCode)." }
        $handle = [HuahaiGlobalSummonProbe]::FindWindowForProcess([uint32]$process.Id)
        if ($handle -ne [IntPtr]::Zero) { break }
    }
    if ($handle -eq [IntPtr]::Zero) { throw 'Background app did not expose a native window handle.' }

    Start-Sleep -Seconds 3
    if ([HuahaiGlobalSummonProbe]::IsWindowVisible($handle)) {
        throw 'Background app became visible before the global summon input.'
    }

    $null = [HuahaiGlobalSummonProbe]::SetCursorPos(900, 500)
    if ($Mode -eq 'RightDoubleClick') { Invoke-RightDoubleClick } else { Invoke-CustomKeyboard }

    $visible = $false
    $topmost = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 100
        $visible = [HuahaiGlobalSummonProbe]::IsWindowVisible($handle)
        $style = [HuahaiGlobalSummonProbe]::GetWindowLongPtr($handle, -20).ToInt64()
        $topmost = ($style -band 0x00000008) -ne 0
        if ($visible -and $topmost) { break }
    }
    if (-not $visible -or -not $topmost) {
        throw "$Mode did not summon a visible topmost panel. Visible=$visible Topmost=$topmost"
    }

    $null = [HuahaiGlobalSummonProbe]::PostMessage($handle, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    $process.Refresh()
    if ([HuahaiGlobalSummonProbe]::IsWindowVisible($handle) -or $process.HasExited) {
        throw 'Closing the summoned panel must return it to the running background process.'
    }

    [pscustomobject]@{
        Status = 'passed'
        Mode = $Mode
        Visible = $visible
        Topmost = $topmost
        ProcessAlive = -not $process.HasExited
    } | ConvertTo-Json -Compress
}
finally {
    $null = [HuahaiGlobalSummonProbe]::SetCursorPos($originalCursor.X, $originalCursor.Y)
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot) -match '^HuahaiClipboard\.GlobalSummon\.[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
