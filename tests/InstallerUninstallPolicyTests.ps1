$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policyScript = Join-Path $projectRoot 'installer\UninstallPolicy.ps1'
if (-not (Test-Path -LiteralPath $policyScript)) {
    throw "Missing uninstall policy: $policyScript"
}

. $policyScript

$localAppData = 'C:\Users\Fixture\AppData\Local'
$installRoot = 'C:\Users\Fixture\AppData\Local\Programs\HuahaiClipboard'
$otherRoot = 'C:\Users\Fixture\AppData\Local\Programs\HuahaiClipboard-copy'

if (-not (Test-HuahaiInstallRoot -InstallRoot $installRoot -LocalAppData $localAppData)) {
    throw 'The exact per-user install directory must be accepted.'
}
if (Test-HuahaiInstallRoot -InstallRoot $otherRoot -LocalAppData $localAppData) {
    throw 'A similarly named directory must be rejected.'
}
if (-not (Test-HuahaiInstallRoot -InstallRoot 'F:\HuahaiClipboard' -LocalAppData $localAppData -ExpectedInstallRoot 'F:\HuahaiClipboard')) {
    throw 'An explicitly registered F drive installation must be accepted.'
}
if (Test-HuahaiInstallRoot -InstallRoot 'F:\HuahaiClipboard' -LocalAppData $localAppData) {
    throw 'An arbitrary F drive path must not be accepted without a matching registration.'
}
if (-not (Test-HuahaiRunValueTargetsInstallRoot -RunValue ('"' + $installRoot + '\HuahaiClipboard.exe" --background') -InstallRoot $installRoot)) {
    throw 'The matching startup command must be owned by this installation.'
}
if (Test-HuahaiRunValueTargetsInstallRoot -RunValue '"C:\Other\HuahaiClipboard.exe" --background' -InstallRoot $installRoot) {
    throw 'An unrelated startup command must be preserved.'
}
if ($null -ne (Get-HuahaiRunValue -RunKeyPath 'HKCU:\Software\HuahaiClipboard\MissingRunKey' -Name 'HuahaiClipboard')) {
    throw 'A missing startup value must be treated as absent without throwing.'
}

[pscustomobject]@{ Status = 'passed'; InstallRoot = $installRoot } | ConvertTo-Json -Compress
