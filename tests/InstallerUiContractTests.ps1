$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bootstrapper = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'installer\Bootstrapper.cs')
$buildScript = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'installer\Build-Installer.ps1')

if ($bootstrapper -notmatch 'ChooseInstallRoot') { throw 'Interactive setup must ask the user to choose an install root.' }
if ($bootstrapper -notmatch 'FolderBrowserDialog') { throw 'Interactive setup must provide a folder browser.' }
if ($bootstrapper -notmatch 'dialog\.Description') { throw 'Interactive setup must visibly describe the install location.' }
if ($bootstrapper -notmatch 'InstallLocationPolicy\.Resolve\(selectedRoot') { throw 'The selected folder must pass the C drive restriction policy.' }
if ($bootstrapper -match 'SpecialFolder\.LocalApplicationData') { throw 'Setup must not default to LocalAppData on C.' }
if ($buildScript -match 'App\.xbf|WindowsAppRuntimeInstall|Assets\\Web') { throw 'Native setup must not package the retired WebView/WinUI runtime.' }
if ($buildScript -notmatch 'HuahaiClipboard\.NativeUiSpike\.exe') { throw 'Native setup must package the approved WPF entry point.' }

[pscustomobject]@{ Status = 'passed'; Surface = 'interactive-install-location' } | ConvertTo-Json -Compress
