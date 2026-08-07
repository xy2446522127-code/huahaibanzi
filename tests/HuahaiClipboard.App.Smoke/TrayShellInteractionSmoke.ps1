param(
    [string]$AppExe = 'F:\HuahaiClipboard\HuahaiClipboard.exe'
)

$ErrorActionPreference = 'Stop'
$resolvedExe = [IO.Path]::GetFullPath($AppExe)
if (-not (Test-Path -LiteralPath $resolvedExe -PathType Leaf)) {
    throw "Installed application was not found: $resolvedExe"
}
if (@(Get-Process HuahaiClipboard -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Tray shell smoke requires no pre-existing HuahaiClipboard process.'
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class HuahaiTrayShellProbe
{
    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string windowName);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

    public static IntPtr FindWindowForProcess(uint expectedProcessId)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr hwnd, IntPtr unused) {
            uint processId;
            GetWindowThreadProcessId(hwnd, out processId);
            if (processId == expectedProcessId) { found = hwnd; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

$vkLWin = [byte]0x5B
$vkB = [byte]0x42
$vkRight = [byte]0x27
$vkReturn = [byte]0x0D
$vkShift = [byte]0x10
$vkF10 = [byte]0x79
$keyUp = [uint32]0x0002
$mouseLeftDown = [uint32]0x0002
$mouseLeftUp = [uint32]0x0004
$mouseRightDown = [uint32]0x0008
$mouseRightUp = [uint32]0x0010
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.TrayShell.' + [guid]::NewGuid().ToString('N'))
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$process = $null
$trayName = -join @([char]0x82B1,[char]0x6D77,[char]0x526A,[char]0x8D34,[char]0x677F)
$showPanelName = -join @([char]0x663E,[char]0x793A,[char]0x9762,[char]0x677F)
$settingsName = -join @([char]0x8BBE,[char]0x7F6E)
$exitName = -join @([char]0x9000,[char]0x51FA)
$hiddenText = -join @([char]0x9690,[char]0x85CF)
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'

Get-ChildItem -LiteralPath $tempBase -Directory -Filter 'HuahaiClipboard.TrayShell.*' -ErrorAction SilentlyContinue | ForEach-Object {
    $stale = [IO.Path]::GetFullPath($_.FullName)
    if ($stale.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        $_.Name -match '^HuahaiClipboard\.TrayShell\.[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $stale -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Send-Key {
    param([byte]$Key)
    [HuahaiTrayShellProbe]::keybd_event($Key, 0, 0, [UIntPtr]::Zero)
    [HuahaiTrayShellProbe]::keybd_event($Key, 0, $keyUp, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 80
}

function Send-WinB {
    [HuahaiTrayShellProbe]::keybd_event($vkLWin, 0, 0, [UIntPtr]::Zero)
    Send-Key $vkB
    [HuahaiTrayShellProbe]::keybd_event($vkLWin, 0, $keyUp, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 250
}

function Get-DirectTrayElement {
    foreach ($className in @('Shell_TrayWnd', 'NotifyIconOverflowWindow')) {
        $handle = [HuahaiTrayShellProbe]::FindWindow($className, $null)
        if ($handle -eq [IntPtr]::Zero) { continue }
        $root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
        $items = $root.FindAll(
            [System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($item in $items) {
            if ($item.Current.Name -like ('*' + $trayName + '*') -and
                -not $item.Current.IsOffscreen -and
                -not $item.Current.BoundingRectangle.IsEmpty) {
                return $item
            }
        }
    }
    $nameCondition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $trayName)
    $topLevels = [System.Windows.Automation.AutomationElement]::RootElement.FindAll(
        [System.Windows.Automation.TreeScope]::Children,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($topLevel in $topLevels) {
        $globalItem = $topLevel.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $nameCondition)
        if ($null -eq $globalItem -or $globalItem.Current.IsOffscreen) { continue }
        $rect = $globalItem.Current.BoundingRectangle
        if (-not $rect.IsEmpty -and $rect.Width -le 100 -and $rect.Height -le 100) {
            return $globalItem
        }
    }
    return $null
}

function Open-TrayOverflow {
    $handle = [HuahaiTrayShellProbe]::FindWindow('Shell_TrayWnd', $null)
    if ($handle -eq [IntPtr]::Zero) { return $false }
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
    $items = $root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $items) {
        if ($item.Current.Name -like ('*' + $hiddenText + '*') -and
            -not $item.Current.IsOffscreen -and -not $item.Current.BoundingRectangle.IsEmpty) {
            Invoke-Element $item
            Start-Sleep -Milliseconds 350
            return $true
        }
    }
    return $false
}

function Focus-TrayIcon {
    $direct = Get-DirectTrayElement
    if ($null -ne $direct) { return $direct }
    if (Open-TrayOverflow) {
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            $direct = Get-DirectTrayElement
            if ($null -ne $direct) { return $direct }
            Start-Sleep -Milliseconds 100
        }
    }
    Send-WinB
    $openedOverflow = $false
    $visited = New-Object System.Collections.Generic.List[string]
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        $focused = [System.Windows.Automation.AutomationElement]::FocusedElement
        $name = if ($null -eq $focused) { '' } else { [string]$focused.Current.Name }
        if (-not [string]::IsNullOrWhiteSpace($name) -and -not $visited.Contains($name)) { $visited.Add($name) }
        if ($name -eq $trayName -or $name -like ('*' + $trayName + '*')) {
            return $focused
        }
        if (-not $openedOverflow -and ($name -like ('*' + $hiddenText + '*') -or $name -match 'Hidden|overflow')) {
            Send-Key $vkReturn
            $openedOverflow = $true
            Start-Sleep -Milliseconds 250
            for ($overflowAttempt = 0; $overflowAttempt -lt 20; $overflowAttempt++) {
                $direct = Get-DirectTrayElement
                if ($null -ne $direct) {
                    try { $direct.SetFocus() } catch { }
                    return $direct
                }
                Start-Sleep -Milliseconds 100
            }
            continue
        }
        Send-Key $vkRight
    }

    $direct = Get-DirectTrayElement
    if ($null -ne $direct) { return $direct }
    throw "Windows notification area did not expose the HuahaiClipboard tray icon. Focused elements: $($visited -join ' | ')"
}

function Invoke-Element {
    param([System.Windows.Automation.AutomationElement]$Element)
    $pattern = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$pattern)) {
        ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
        return
    }
    $rect = $Element.Current.BoundingRectangle
    if ($rect.IsEmpty) { throw "Automation element has no clickable rectangle: $($Element.Current.Name)" }
    $x = [int]($rect.Left + $rect.Width / 2)
    $y = [int]($rect.Top + $rect.Height / 2)
    [HuahaiTrayShellProbe]::SetCursorPos($x, $y) | Out-Null
    [HuahaiTrayShellProbe]::mouse_event($mouseLeftDown, 0, 0, 0, [UIntPtr]::Zero)
    [HuahaiTrayShellProbe]::mouse_event($mouseLeftUp, 0, 0, 0, [UIntPtr]::Zero)
}

function Invoke-TrayMenuItem {
    param([string]$Name)
    try { $icon = Focus-TrayIcon }
    catch { throw "While opening tray item '$Name': $($_.Exception.Message)" }
    $iconRect = $icon.Current.BoundingRectangle
    if (-not $iconRect.IsEmpty) {
        [HuahaiTrayShellProbe]::SetCursorPos([int]($iconRect.Left + $iconRect.Width / 2), [int]($iconRect.Top + $iconRect.Height / 2)) | Out-Null
        [HuahaiTrayShellProbe]::mouse_event($mouseRightDown, 0, 0, 0, [UIntPtr]::Zero)
        [HuahaiTrayShellProbe]::mouse_event($mouseRightUp, 0, 0, 0, [UIntPtr]::Zero)
    }
    else {
        [HuahaiTrayShellProbe]::keybd_event($vkShift, 0, 0, [UIntPtr]::Zero)
        Send-Key $vkF10
        [HuahaiTrayShellProbe]::keybd_event($vkShift, 0, $keyUp, [UIntPtr]::Zero)
    }
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $item = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Descendants,
            $condition)
        if ($null -ne $item -and -not $item.Current.IsOffscreen) {
            Invoke-Element $item
            return
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Tray context menu item was not exposed by Windows UI Automation: $Name"
}

function Wait-WindowState {
    param([bool]$Visible, [bool]$Topmost, [int]$MinimumWidth = 0, [IntPtr]$WindowHandle = [IntPtr]::Zero)
    $lastState = $null
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        $handle = if ($WindowHandle -ne [IntPtr]::Zero) { $WindowHandle } else { [HuahaiTrayShellProbe]::FindWindowForProcess([uint32]$process.Id) }
        if ($handle -ne [IntPtr]::Zero) {
            $isVisible = [HuahaiTrayShellProbe]::IsWindowVisible($handle)
            $style = [HuahaiTrayShellProbe]::GetWindowLongPtr($handle, -20).ToInt64()
            $isTopmost = ($style -band 0x00000008) -ne 0
            $rect = New-Object HuahaiTrayShellProbe+Rect
            [HuahaiTrayShellProbe]::GetWindowRect($handle, [ref]$rect) | Out-Null
            $width = $rect.Right - $rect.Left
            $lastState = "Handle=$handle Visible=$isVisible Topmost=$isTopmost Width=$width"
            if ($isVisible -eq $Visible -and $isTopmost -eq $Topmost -and $width -ge $MinimumWidth) {
                return [pscustomobject]@{ Handle = $handle; Width = $width; Visible = $isVisible; Topmost = $isTopmost }
            }
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Installed window did not reach Visible=$Visible Topmost=$Topmost MinimumWidth=$MinimumWidth. Last=$lastState"
}

function Start-TestProcess {
    $script:process = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    if ($script:process.HasExited) { throw "Background application exited early: $($script:process.ExitCode)" }
}

function Stop-TestProcess {
    if ($null -ne $script:process -and -not $script:process.HasExited) {
        Stop-Process -Id $script:process.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $script:process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $script:process = $null
    Start-Sleep -Milliseconds 500
}

try {
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
    Start-TestProcess

    Invoke-TrayMenuItem $showPanelName
    $panel = Wait-WindowState -Visible $true -Topmost $true -MinimumWidth 400
    [HuahaiTrayShellProbe]::ShowWindow($panel.Handle, 0) | Out-Null
    Start-Sleep -Milliseconds 250

    Invoke-TrayMenuItem $settingsName
    $settings = Wait-WindowState -Visible $true -Topmost $true -MinimumWidth 700
    [HuahaiTrayShellProbe]::ShowWindow($settings.Handle, 0) | Out-Null
    Start-Sleep -Milliseconds 250

    Invoke-TrayMenuItem $exitName
    if (-not $process.WaitForExit(10000)) {
        throw 'Tray exit menu item did not terminate the background process.'
    }

    [pscustomobject]@{
        Status = 'passed'
        PanelVisibleTopmost = $true
        SettingsVisibleTopmost = $true
        SettingsWidth = $settings.Width
        ProcessExited = $true
    } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot) -match '^HuahaiClipboard\.TrayShell\.[0-9a-f]{32}$' -and
        (Test-Path -LiteralPath $resolvedTestRoot)) {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
