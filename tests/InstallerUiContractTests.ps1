$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bootstrapper = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'installer\Bootstrapper.cs')
$buildScript = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'installer\Build-Installer.ps1')

if ($bootstrapper -notmatch 'ChooseInstallRoot') { throw 'Interactive setup must ask the user to choose an install root.' }
if ($bootstrapper -notmatch 'FolderBrowserDialog') { throw 'Interactive setup must provide a folder browser.' }
if ($bootstrapper -notmatch 'dialog\.Description') { throw 'Interactive setup must visibly describe the install location.' }
if ($bootstrapper -notmatch 'InstallLocationPolicy\.Resolve\(selectedRoot') { throw 'The selected folder must pass the C drive restriction policy.' }
if ($bootstrapper -notmatch 'InstallLocationPolicy\.DefaultForRoots\(GetAvailableFixedDriveRoots\(\), ProductFolderName\)') {
    throw 'Setup must derive its default install root from available fixed drives through the C-drive restriction policy.'
}
if ($buildScript -notmatch 'App\.xbf') { throw 'WebView setup must package WinUI compiled resources.' }
if ($buildScript -notmatch 'WindowsAppRuntimeInstall') { throw 'WebView setup must package the Windows App Runtime prerequisite.' }
if ($buildScript -notmatch 'MicrosoftEdgeWebView2RuntimeInstallerX64') { throw 'WebView setup must package the Evergreen WebView2 Runtime prerequisite.' }
if ($bootstrapper -notmatch 'MicrosoftEdgeWebView2RuntimeInstallerX64') { throw 'Setup must require the packaged Evergreen WebView2 Runtime prerequisite.' }
if ($bootstrapper -notmatch 'NeedsWebView2Runtime') { throw 'Setup must detect whether the Evergreen WebView2 Runtime is already available.' }
if ($bootstrapper -notmatch 'GetInstalledWebView2RuntimeVersions') { throw 'Setup must query installed Evergreen WebView2 Runtime versions.' }
if ($bootstrapper -notmatch '\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5\}') { throw 'Setup must query the current Evergreen WebView2 Runtime product registration.' }
if ($buildScript -notmatch 'Assets\\Web\\product-shell\.html') { throw 'WebView setup must package the approved offline product shell.' }
if ($buildScript -notmatch 'HuahaiClipboard\.App\.exe') { throw 'WebView setup must package the formal WinUI entry point.' }
if ($buildScript -match 'HuahaiClipboard\.NativeUiSpike\.exe') { throw 'The WPF rollback entry must not be renamed into the release executable.' }
$runtimeWaitIndex = $bootstrapper.IndexOf('WaitForExit(15000)', [StringComparison]::Ordinal)
$runtimeReadIndex = $bootstrapper.IndexOf('StandardOutput.ReadToEnd()', [StringComparison]::Ordinal)
if ($runtimeWaitIndex -lt 0 -or $runtimeReadIndex -lt 0 -or $runtimeWaitIndex -gt $runtimeReadIndex) {
    throw 'Windows App Runtime detection must apply its timeout before reading redirected output.'
}
if ($bootstrapper -notmatch "Microsoft\.WindowsAppRuntime\.1\.7") { throw 'Setup must query the exact side-by-side Windows App Runtime 1.7 family.' }
if ($bootstrapper -notmatch "Architecture -in @\('X64','Neutral'\)") { throw 'Setup must reject incompatible Windows App Runtime architectures.' }

[pscustomobject]@{ Status = 'passed'; Surface = 'interactive-install-location' } | ConvertTo-Json -Compress
