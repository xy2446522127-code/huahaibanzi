param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

$ErrorActionPreference = 'Stop'

Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class HuahaiNativeUiSpikeWindowProbe
{
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
'@

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$ownedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$first = $null
$expectedWindowTitle = -join [char[]](0x82B1, 0x6D77, 0x526A, 0x8D34, 0x677F)

function Find-OwnedWindow([int]$ProcessId) {
    $script:foundHandle = [IntPtr]::Zero
    [HuahaiNativeUiSpikeWindowProbe]::EnumWindows({
        param([IntPtr]$handle, [IntPtr]$state)
        $candidateProcessId = 0
        [HuahaiNativeUiSpikeWindowProbe]::GetWindowThreadProcessId($handle, [ref]$candidateProcessId) | Out-Null
        if ($candidateProcessId -eq $ProcessId) {
            $title = [System.Text.StringBuilder]::new(128)
            [HuahaiNativeUiSpikeWindowProbe]::GetWindowText($handle, $title, $title.Capacity) | Out-Null
            if ($title.ToString() -eq $expectedWindowTitle) {
                $script:foundHandle = $handle
                return $false
            }
        }

        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $script:foundHandle
}

function Wait-Until([scriptblock]$Condition, [int]$TimeoutMilliseconds = 5000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds 25
    } while ([DateTime]::UtcNow -lt $deadline)
    return $false
}

try {
    $first = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -PassThru
    $ownedProcesses.Add($first)
    if (-not $first.WaitForInputIdle(5000)) { throw 'Background process did not become input-idle' }

    $windowFound = Wait-Until { (Find-OwnedWindow $first.Id) -ne [IntPtr]::Zero }
    if (-not $windowFound) { throw 'Background process did not pre-create its window' }
    $handle = Find-OwnedWindow $first.Id
    $hiddenInitially = -not [HuahaiNativeUiSpikeWindowProbe]::IsWindowVisible($handle)
    if (-not $hiddenInitially) { throw 'Background launch must pre-create a hidden window' }

    $second = Start-Process -FilePath $resolvedExe -PassThru
    $ownedProcesses.Add($second)
    if (-not $second.WaitForExit(5000)) { throw 'Second instance did not signal and exit' }

    $summonedVisible = Wait-Until { [HuahaiNativeUiSpikeWindowProbe]::IsWindowVisible($handle) }
    if (-not $summonedVisible) { throw 'Second instance did not summon the first window' }

    $GwlExStyle = -20
    $WsExTopmost = 0x00000008
    $summonedTopmost = (([HuahaiNativeUiSpikeWindowProbe]::GetWindowLongPtr($handle, $GwlExStyle).ToInt64() -band $WsExTopmost) -ne 0)
    if (-not $summonedTopmost) { throw 'Summoning must show a topmost panel' }

    $WmClose = 0x0010
    [HuahaiNativeUiSpikeWindowProbe]::PostMessage($handle, $WmClose, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    $hiddenAfterClose = Wait-Until { -not [HuahaiNativeUiSpikeWindowProbe]::IsWindowVisible($handle) }
    $first.Refresh()
    $processAlive = -not $first.HasExited
    $hiddenTopmost = (([HuahaiNativeUiSpikeWindowProbe]::GetWindowLongPtr($handle, $GwlExStyle).ToInt64() -band $WsExTopmost) -ne 0)

    if (-not $hiddenAfterClose) { throw 'WM_CLOSE must hide the panel' }
    if (-not $processAlive) { throw 'WM_CLOSE must keep the background process alive' }
    if ($hiddenTopmost) { throw 'Hidden panel must not remain topmost' }

    [pscustomobject]@{
        SummonedTopmost = $summonedTopmost
        SummonedVisible = $summonedVisible
        HiddenTopmost = $hiddenTopmost
        HiddenVisible = -not $hiddenAfterClose
        ProcessAlive = $processAlive
    } | ConvertTo-Json -Compress
}
finally {
    foreach ($process in $ownedProcesses) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
        catch {
        }
    }
}
