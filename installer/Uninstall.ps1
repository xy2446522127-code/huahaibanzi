param([switch]$Silent, [switch]$RemoveData)

$ErrorActionPreference = 'Stop'
function ConvertFrom-HuahaiUtf8Base64([string]$value) {
    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($value))
}

$productName = ConvertFrom-HuahaiUtf8Base64 '6Iqx5rW35Ymq6LS05p2/'
$installRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HuahaiClipboard'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$dataRoot = Join-Path $installRoot 'Data'
. (Join-Path $PSScriptRoot 'UninstallPolicy.ps1')

$registeredInstallRoot = Get-ItemPropertyValue -LiteralPath $uninstallKey -Name 'InstallLocation' -ErrorAction Stop
if (-not (Test-HuahaiInstallRoot -InstallRoot $installRoot -LocalAppData $env:LOCALAPPDATA -ExpectedInstallRoot $registeredInstallRoot)) {
    throw (ConvertFrom-HuahaiUtf8Base64 '5Y246L2955uu5b2V5qCh6aqM5aSx6LSl77yM5pyq5Yig6Zmk5Lu75L2V5paH5Lu244CC')
}

if (-not $Silent) {
    Add-Type -AssemblyName System.Windows.Forms
    $prompt = if ($RemoveData) {
        (ConvertFrom-HuahaiUtf8Base64 '56Gu5a6a5Y246L296Iqx5rW35Ymq6LS05p2/5bm25rC45LmF5Yig6Zmk5YWo6YOo5pys5py65pWw5o2u5ZCX77yf') +
            "`n`n" +
            (ConvertFrom-HuahaiUtf8Base64 '5Yig6Zmk5ZCO5peg5rOV5oGi5aSN77ya') +
            "`n$dataRoot"
    } else {
        (ConvertFrom-HuahaiUtf8Base64 '56Gu5a6a5Y246L296Iqx5rW35Ymq6LS05p2/5ZCX77yf') +
            "`n`n" +
            (ConvertFrom-HuahaiUtf8Base64 '5Y6G5Y+y6K6w5b2V5ZKM6K6+572u5bCG5L+d55WZ5Zyo77ya') +
            "`n$dataRoot"
    }
    $choice = [System.Windows.Forms.MessageBox]::Show(
        $prompt,
        $productName,
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }
}

$normalizedRoot = $installRoot.TrimEnd('\') + '\'
Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $path = $_.Path
        if ($path -and $path.StartsWith($normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            $_.CloseMainWindow() | Out-Null
            if (-not $_.WaitForExit(1200)) {
                $_.Kill()
                $_.WaitForExit(3000)
            }
        }
    } catch { }
}

$removalTargets = @(Get-HuahaiRemovalTargets -InstallRoot $installRoot -RemoveData:$RemoveData)
foreach ($target in $removalTargets) {
    $removed = $false
    for ($attempt = 0; $attempt -lt 10 -and -not $removed; $attempt++) {
        try {
            Remove-Item -LiteralPath $target -Recurse -Force
            $removed = -not (Test-Path -LiteralPath $target)
        } catch {
            Start-Sleep -Milliseconds 200
        }
    }
    if (-not $removed) {
        throw (ConvertFrom-HuahaiUtf8Base64 '5bqU55So5paH5Lu25q2j6KKr5Y2g55So77yM6K+35YWz6Zet6Iqx5rW35Ymq6LS05p2/5ZCO6YeN6K+V44CC')
    }
}

if ($RemoveData -and (Test-Path -LiteralPath $installRoot)) {
    Remove-Item -LiteralPath $installRoot -Force -ErrorAction SilentlyContinue
}

$shortcutDirectories = @(
    (Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'),
    ([Environment]::GetFolderPath('DesktopDirectory'))
)
$shell = New-Object -ComObject WScript.Shell
try {
    foreach ($shortcutDirectory in $shortcutDirectories) {
        Get-ChildItem -LiteralPath $shortcutDirectory -Filter '*.lnk' -File -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                $shortcut = $shell.CreateShortcut($_.FullName)
                if ($shortcut.TargetPath.StartsWith($normalizedRoot, [StringComparison]::OrdinalIgnoreCase)) {
                    Remove-Item -LiteralPath $_.FullName -Force
                }
            } catch { }
        }
    }
}
finally {
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

$runValue = Get-HuahaiRunValue -RunKeyPath $runKey -Name 'HuahaiClipboard'
if (Test-HuahaiRunValueTargetsInstallRoot -RunValue $runValue -InstallRoot $installRoot) {
    Remove-ItemProperty -LiteralPath $runKey -Name 'HuahaiClipboard' -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue

if (-not $Silent) {
    $message = if ($RemoveData) {
        ConvertFrom-HuahaiUtf8Base64 '6Iqx5rW35Ymq6LS05p2/5Y+K5YW25pys5py65pWw5o2u5bey5YWo6YOo5Yig6Zmk44CC'
    } else {
        (ConvertFrom-HuahaiUtf8Base64 '6Iqx5rW35Ymq6LS05p2/5bey5Y246L2944CC') +
            "`n`n" +
            (ConvertFrom-HuahaiUtf8Base64 '5Y6G5Y+y6K6w5b2V5ZKM6K6+572u5LuN5L+d55WZ5Zyo77ya') +
            "`n$dataRoot"
    }
    [System.Windows.Forms.MessageBox]::Show(
        $message,
        $productName,
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
