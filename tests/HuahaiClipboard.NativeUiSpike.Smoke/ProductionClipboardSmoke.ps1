param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HuahaiClipboardInputProbe
{
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
'@

$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$temporaryBase = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$dataRoot = Join-Path $temporaryBase ('HuahaiClipboard-Smoke-' + [guid]::NewGuid().ToString('N'))
$historyPath = Join-Path $dataRoot 'HuahaiClipboard\history.dat'
$uniqueText = 'huahai-production-smoke-' + [guid]::NewGuid().ToString('N')
$previousText = Get-Clipboard -Raw -TextFormatType Text -ErrorAction SilentlyContinue
$previousOverride = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$process = $null

function Wait-Until([scriptblock]$Condition, [int]$TimeoutMilliseconds = 7000) {
    $deadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        if (& $Condition) { return $true }
        Start-Sleep -Milliseconds 25
    } while ([DateTime]::UtcNow -lt $deadline)
    return $false
}

function Test-ClipboardText([string]$Expected) {
    try {
        return (Get-Clipboard -Raw -TextFormatType Text -ErrorAction Stop) -eq $Expected
    }
    catch [System.Runtime.InteropServices.ExternalException] {
        return $false
    }
}

try {
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $dataRoot
    $process = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -PassThru
    if (-not $process.WaitForInputIdle(5000)) { throw 'Production process did not become input-idle.' }
    Start-Sleep -Milliseconds 1000

    Set-Clipboard -Value $uniqueText
    Start-Sleep -Milliseconds 300

    $signal = [System.Threading.EventWaitHandle]::OpenExisting('Local\HuahaiClipboard.NativeUiSpike.Activate')
    try { $signal.Set() | Out-Null } finally { $signal.Dispose() }

    $window = $null
    if (-not (Wait-Until {
        $root = [System.Windows.Automation.AutomationElement]::RootElement
        $condition = [System.Windows.Automation.PropertyCondition]::new(
            [System.Windows.Automation.AutomationElement]::ProcessIdProperty,
            $process.Id)
        $script:window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        return $null -ne $script:window
    })) { throw 'Summoned production window was not found by UI Automation.' }

    $textCondition = [System.Windows.Automation.PropertyCondition]::new(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $uniqueText)
    $textElement = $null
    if (-not (Wait-Until {
        $script:textElement = $window.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $textCondition)
        return $null -ne $script:textElement
    })) {
        $available = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { try { $_.Current.Name } catch { '' } } |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
            Select-Object -First 30
        throw ('Captured clipboard text did not appear in the native panel. Visible names: ' + ($available -join ' | '))
    }

    $liveCountPrefix = -join [char[]](0x6700, 0x8FD1, 0x20, 0x37, 0x20, 0x5929, 0x20, 0x00B7, 0x20)
    $liveCountSuffix = -join [char[]](0x20, 0x6761)
    if (-not (Wait-Until {
        $script:liveCountElement = $window.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) |
            Where-Object {
                try {
                    $name = $_.Current.Name
                    if (-not $name.StartsWith($liveCountPrefix, [StringComparison]::Ordinal) -or
                        -not $name.EndsWith($liveCountSuffix, [StringComparison]::Ordinal)) { return $false }
                    $countText = $name.Substring(
                        $liveCountPrefix.Length,
                        $name.Length - $liveCountPrefix.Length - $liveCountSuffix.Length)
                    $count = 0
                    return [int]::TryParse($countText, [ref]$count) -and $count -ge 1
                }
                catch { return $false }
            } |
            Select-Object -First 1
        return $null -ne $script:liveCountElement
    })) { throw 'The header did not update to the real-time history count.' }

    Set-Clipboard -Value 'huahai-before-row-click'
    Start-Sleep -Milliseconds 300
    $textElement = $null
    if (-not (Wait-Until {
        $script:textElement = $window.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $textCondition)
        return $null -ne $script:textElement
    })) { throw 'Captured record was not available after the live history refresh.' }

    $bounds = $textElement.Current.BoundingRectangle
    [HuahaiClipboardInputProbe]::SetCursorPos(
        [int]($bounds.Left + $bounds.Width / 2),
        [int]($bounds.Top + $bounds.Height / 2)) | Out-Null
    [HuahaiClipboardInputProbe]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [HuahaiClipboardInputProbe]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)

    if (-not (Wait-Until { Test-ClipboardText $uniqueText })) {
        throw 'Clicking the native record did not write the historical value back to the clipboard.'
    }

    [pscustomobject]@{
        Captured = $true
        Displayed = $true
        LiveCount = $true
        CopiedBack = $true
        HistoryFile = (Test-Path -LiteralPath $historyPath)
    } | ConvertTo-Json -Compress
}
finally {
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousOverride
    if ($null -ne $process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        } catch {}
    }
    if ($null -ne $previousText) { Set-Clipboard -Value $previousText }
    $resolvedDataRoot = [System.IO.Path]::GetFullPath($dataRoot)
    if ($resolvedDataRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase) -and
        [System.IO.Path]::GetFileName($resolvedDataRoot).StartsWith('HuahaiClipboard-Smoke-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $resolvedDataRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
