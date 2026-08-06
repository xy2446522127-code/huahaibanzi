param(
    [Parameter(Mandatory = $true)]
    [string] $ExePath,

    [int] $TimeoutSeconds = 12,

    [switch] $StartHidden
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path

Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiTransientWindowProbe {
    public delegate bool EnumWindowsCallback(IntPtr windowHandle, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern IntPtr GetWindowLongPtr(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

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

$extendedStyleIndex = -20
$topmostStyle = 0x00000008
$closeMessage = 0x0010
$process = if ($StartHidden) {
    Start-Process -FilePath $resolvedExe -ArgumentList '--background' -WorkingDirectory (Split-Path $resolvedExe) -PassThru
} else {
    Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
}

try {
    $handle = [IntPtr]::Zero
    for ($attempt = 0; $attempt -lt ($TimeoutSeconds * 2); $attempt++) {
        Start-Sleep -Milliseconds 500
        $process.Refresh()
        if ($process.HasExited) {
            throw "Application exited before the window was ready. ExitCode=$($process.ExitCode)"
        }

        $candidateHandle = [HuahaiTransientWindowProbe]::FindWindowForProcess([uint32]$process.Id)
        if ($candidateHandle -ne [IntPtr]::Zero) {
            $handle = $candidateHandle
            break
        }
    }

    if ($handle -eq [IntPtr]::Zero) {
        throw 'Application did not expose a top-level window.'
    }

    # The WinUI handle exists before WebView2 and the single-instance activation path are ready.
    Start-Sleep -Seconds 3
    if ($StartHidden -and [HuahaiTransientWindowProbe]::IsWindowVisible($handle)) {
        throw 'A --background launch must remain hidden before the summon activation.'
    }
    if ($StartHidden) {
        $backgroundActivation = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
        if (-not $backgroundActivation.WaitForExit($TimeoutSeconds * 1000) -or $backgroundActivation.ExitCode -ne 0) {
            throw 'A second --background launch did not exit cleanly.'
        }
        Start-Sleep -Milliseconds 500
        if ([HuahaiTransientWindowProbe]::IsWindowVisible($handle)) {
            throw 'A second --background launch must not summon the hidden panel.'
        }
    }
    $summon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    if (-not $summon.WaitForExit($TimeoutSeconds * 1000)) {
        throw 'The second launch did not redirect its activation to the background instance.'
    }
    $summonExitCode = $summon.ExitCode

    $shownTopmost = $false
    $shownVisible = $false
    for ($attempt = 0; $attempt -lt ($TimeoutSeconds * 4); $attempt++) {
        Start-Sleep -Milliseconds 250
        $shownStyle = [HuahaiTransientWindowProbe]::GetWindowLongPtr($handle, $extendedStyleIndex).ToInt64()
        $shownTopmost = ($shownStyle -band $topmostStyle) -ne 0
        $shownVisible = [HuahaiTransientWindowProbe]::IsWindowVisible($handle)
        if ($shownTopmost -and $shownVisible) {
            break
        }
    }
    if (-not $shownTopmost -or -not $shownVisible) {
        $process.Refresh()
        throw "Summoning must show the panel as a topmost window. Handle=$handle MainHandle=$($process.MainWindowHandle) Visible=$shownVisible Topmost=$shownTopmost Style=0x$('{0:X}' -f $shownStyle) SecondExitCode=$summonExitCode"
    }

    $null = [HuahaiTransientWindowProbe]::PostMessage(
        $handle,
        $closeMessage,
        [IntPtr]::Zero,
        [IntPtr]::Zero)
    Start-Sleep -Seconds 1
    $process.Refresh()
    $hiddenStyle = [HuahaiTransientWindowProbe]::GetWindowLongPtr($handle, $extendedStyleIndex).ToInt64()
    $hiddenTopmost = ($hiddenStyle -band $topmostStyle) -ne 0
    $hiddenVisible = [HuahaiTransientWindowProbe]::IsWindowVisible($handle)
    if ($hiddenTopmost -or $hiddenVisible -or $process.HasExited) {
        throw 'Closing to the background must hide the panel, remove topmost, and keep the process alive.'
    }

    [pscustomobject]@{
        Status = 'passed'
        ProcessId = $process.Id
        SummonedTopmost = $shownTopmost
        SummonedVisible = $shownVisible
        HiddenTopmost = $hiddenTopmost
        HiddenVisible = $hiddenVisible
        ProcessAlive = -not $process.HasExited
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
}
