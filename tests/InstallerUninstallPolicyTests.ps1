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
if (-not (Test-HuahaiRunValueTargetsInstallRoot -RunValue ('"' + $installRoot + '\HuahaiClipboard.App.exe" --background') -InstallRoot $installRoot)) {
    throw 'The matching startup command must be owned by this installation.'
}
if (-not (Test-HuahaiRunValueTargetsInstallRoot -RunValue ('"' + $installRoot + '\HuahaiClipboard.exe" --background') -InstallRoot $installRoot)) {
    throw 'The legacy startup command must remain removable during upgrades.'
}
if (Test-HuahaiRunValueTargetsInstallRoot -RunValue '"C:\Other\HuahaiClipboard.exe" --background' -InstallRoot $installRoot) {
    throw 'An unrelated startup command must be preserved.'
}
if ($null -ne (Get-HuahaiRunValue -RunKeyPath 'HKCU:\Software\HuahaiClipboard\MissingRunKey' -Name 'HuahaiClipboard')) {
    throw 'A missing startup value must be treated as absent without throwing.'
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.UninstallPolicy.' + [guid]::NewGuid().ToString('N'))
try {
    $fixtureInstall = Join-Path $fixtureRoot 'HuahaiClipboard'
    $fixtureData = Join-Path $fixtureInstall 'Data\S-1-5-21-1000'
    New-Item -ItemType Directory -Path $fixtureData -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $fixtureInstall 'HuahaiClipboard.App.exe') -Value 'app'
    Set-Content -LiteralPath (Join-Path $fixtureData 'settings.json') -Value 'data'

    $preservingTargets = @(Get-HuahaiRemovalTargets -InstallRoot $fixtureInstall)
    if ($preservingTargets -contains (Join-Path $fixtureInstall 'Data')) {
        throw 'Default uninstall must preserve the install-root Data directory.'
    }
    if ($preservingTargets -notcontains (Join-Path $fixtureInstall 'HuahaiClipboard.App.exe')) {
        throw 'Default uninstall must remove application files.'
    }

    $fullRemovalTargets = @(Get-HuahaiRemovalTargets -InstallRoot $fixtureInstall -RemoveData)
    if ($fullRemovalTargets -notcontains (Join-Path $fixtureInstall 'Data')) {
        throw 'RemoveData uninstall must include the install-root Data directory.'
    }
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
}

[pscustomobject]@{ Status = 'passed'; InstallRoot = $installRoot } | ConvertTo-Json -Compress
