param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [ValidateRange(0, 15000)]
    [int]$StabilizationMilliseconds = 350,

    [switch]$OpenSettings,

    [int]$DebugPort = 9236
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class HuahaiNativeVisualCapture
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
$resolvedOutput = [IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $resolvedOutput
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ("HuahaiClipboard.VisualParity.{0}" -f [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$previousTestDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
$previousBrowserArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
if ($OpenSettings) { $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort" }
$process = $null

function Find-OwnedVisibleWindow([int]$OwnedProcessId) {
    $script:foundHandle = [IntPtr]::Zero
    [HuahaiNativeVisualCapture]::EnumWindows({
        param([IntPtr]$handle, [IntPtr]$state)
        $candidateProcessId = 0
        [HuahaiNativeVisualCapture]::GetWindowThreadProcessId($handle, [ref]$candidateProcessId) | Out-Null
        if ($candidateProcessId -eq $OwnedProcessId -and [HuahaiNativeVisualCapture]::IsWindowVisible($handle)) {
            $script:foundHandle = $handle
            return $false
        }

        return $true
    }, [IntPtr]::Zero) | Out-Null
    return $script:foundHandle
}

try {
    $process = Start-Process -FilePath $resolvedExe -PassThru
    if (-not $process.WaitForInputIdle(5000)) { throw 'Native panel did not become input-idle' }

    $deadline = [DateTime]::UtcNow.AddSeconds(8)
    $handle = [IntPtr]::Zero
    do {
        $handle = Find-OwnedVisibleWindow $process.Id
        if ($handle -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 50 }
    } while ($handle -eq [IntPtr]::Zero -and [DateTime]::UtcNow -lt $deadline)

    if ($handle -eq [IntPtr]::Zero) { throw 'Native panel did not become visible' }
    if ($OpenSettings) {
        $projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
        $probe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
        for ($attempt = 0; $attempt -lt 40; $attempt++) {
            try {
                $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
                break
            } catch {
                if ($attempt -eq 39) { throw 'WebView2 debugging endpoint did not become ready.' }
                Start-Sleep -Milliseconds 250
            }
        }
        $settingsResult = node $probe $DebugPort open-settings
        if ($LASTEXITCODE -ne 0) { throw "Settings surface did not open: $settingsResult" }
    }
    Start-Sleep -Milliseconds $StabilizationMilliseconds

    $rect = New-Object HuahaiNativeVisualCapture+RECT
    [HuahaiNativeVisualCapture]::GetWindowRect($handle, [ref]$rect) | Out-Null
    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    $bitmap = New-Object Drawing.Bitmap $width, $height
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, [Drawing.Size]::new($width, $height))
    }
    finally {
        $graphics.Dispose()
    }

    try {
        $bitmap.Save($resolvedOutput, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $bitmap.Dispose()
    }

    [pscustomobject]@{
        Path = $resolvedOutput
        Width = $width
        Height = $height
    } | ConvertTo-Json -Compress
}
finally {
    if ($process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
        catch {
        }
    }

    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    $safeLeaf = (Split-Path $resolvedTestRoot -Leaf).StartsWith('HuahaiClipboard.VisualParity.')
    if ($safeLeaf -and $resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousTestDataRoot
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousBrowserArguments
}
