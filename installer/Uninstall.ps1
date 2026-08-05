param([switch]$Silent)

$ErrorActionPreference = 'Stop'
$productName = '花海剪贴板'
$installRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$appPath = Join-Path $installRoot 'HuahaiClipboard.exe'
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\HuahaiClipboard'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$dataRoot = Join-Path $env:LOCALAPPDATA 'HuahaiClipboard'
. (Join-Path $PSScriptRoot 'UninstallPolicy.ps1')

$registeredInstallRoot = Get-ItemPropertyValue -LiteralPath $uninstallKey -Name 'InstallLocation' -ErrorAction Stop
if (-not (Test-HuahaiInstallRoot -InstallRoot $installRoot -LocalAppData $env:LOCALAPPDATA -ExpectedInstallRoot $registeredInstallRoot)) {
    throw '卸载目录校验失败，未删除任何文件。'
}

if (-not $Silent) {
    Add-Type -AssemblyName System.Windows.Forms
    $choice = [System.Windows.Forms.MessageBox]::Show(
        "确定卸载花海剪贴板吗？`n`n历史记录和设置将保留在：`n$dataRoot",
        $productName,
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question)
    if ($choice -ne [System.Windows.Forms.DialogResult]::Yes) { exit 0 }
}

$normalizedRoot = $installRoot.TrimEnd('\') + '\'
Get-Process -ErrorAction SilentlyContinue | ForEach-Object {
    try {
        $path = $_.Path
        if ($path -and $path.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $_.CloseMainWindow() | Out-Null
            if (-not $_.WaitForExit(1200)) {
                $_.Kill()
                $_.WaitForExit(3000)
            }
        }
    } catch { }
}

$removed = $false
for ($attempt = 0; $attempt -lt 10 -and -not $removed; $attempt++) {
    try {
        Remove-Item -LiteralPath $installRoot -Recurse -Force
        $removed = -not (Test-Path -LiteralPath $installRoot)
    } catch {
        Start-Sleep -Milliseconds 200
    }
}

if (-not $removed) {
    throw '应用文件正被占用，请关闭花海剪贴板后重试。'
}

# 仅在程序目录成功删除后清理快捷方式、开机自启和卸载入口。
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
                if ($shortcut.TargetPath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                    Remove-Item -LiteralPath $_.FullName -Force
                }
            } catch { }
        }
    }
}
finally {
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
}

$runValue = Get-HuahaiRunValue -RunKeyPath $runKey -Name 'HuahaiClipboard'
if (Test-HuahaiRunValueTargetsInstallRoot -RunValue $runValue -InstallRoot $installRoot) {
    Remove-ItemProperty -LiteralPath $runKey -Name 'HuahaiClipboard' -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath $uninstallKey -Recurse -Force -ErrorAction SilentlyContinue

if (-not $Silent) {
    [System.Windows.Forms.MessageBox]::Show(
        "花海剪贴板已卸载。`n`n历史记录和设置仍保留在：`n$dataRoot",
        $productName,
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Information) | Out-Null
}
