$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$adapterRoot = Join-Path $PSScriptRoot 'update-evidence'
$newInstaller = Join-Path $projectRoot 'dist\HuahaiClipboard-Setup.exe'
$oldInstaller = Join-Path $projectRoot 'dist\HuahaiClipboard-Setup-1.1.10.exe'
$newHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $newInstaller).Hash.ToLowerInvariant()
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $oldInstaller).Hash.ToLowerInvariant()
$publisher = 'CN=HuahaiClipboard Open Source Release'

function Invoke-Adapter {
    param([Parameter(Mandatory = $true)][string]$Script, [Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & powershell.exe -NoProfile -NonInteractive -File (Join-Path $adapterRoot $Script) @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Script failed with exit code $LASTEXITCODE" }
    return $output | ConvertFrom-Json
}

$signature = Invoke-Adapter 'WindowsAuthenticode.ps1' @(
    'package_signature', $newInstaller, $newHash, $publisher)
if (-not $signature.passed -or $signature.trust_status -ne 'trusted') {
    throw 'The Authenticode evidence adapter did not verify the release package.'
}

$integrity = Invoke-Adapter 'UpdateSecurityFixture.ps1' @(
    'integrity_rejection', $newInstaller, $newHash)
if (-not $integrity.passed -or @($integrity.blocked_cases).Count -lt 4) {
    throw 'The update security evidence adapter did not cover every required rejection.'
}

$transaction = Invoke-Adapter 'InstallerFaultInjection.ps1' @(
    'transaction_rollback', 'atomic-directory-swap', 'install/current', 'install/staging', 'install/backup')
if (-not $transaction.passed -or @($transaction.scenarios).Count -lt 3) {
    throw 'The transaction evidence adapter did not cover every rollback scenario.'
}

$data = Invoke-Adapter 'InstallerDataFixture.ps1' @(
    'data_preservation', '<install-root>/Data/<Windows-SID>')
if (-not $data.passed -or -not $data.upgrade_preserved -or -not $data.rollback_preserved) {
    throw 'The data evidence adapter did not prove preservation.'
}

$upgrade = Invoke-Adapter 'IsolatedWindowsUpgrade.ps1' @(
    'installed_upgrade', $oldInstaller, $oldHash, $newInstaller, $newHash, '1.1.10', '1.1.11', 'x64')
if (-not $upgrade.passed -or -not $upgrade.user_data_preserved -or -not $upgrade.startup_succeeded) {
    throw 'The isolated upgrade evidence adapter did not prove the N-1 to N path.'
}

$startup = Invoke-Adapter 'InstalledStartupSmoke.ps1' @(
    'post_update_startup', $newInstaller, $newHash, '1.1.11')
if (-not $startup.passed -or -not $startup.startup_succeeded -or @($startup.criteria).Count -lt 2) {
    throw 'The startup evidence adapter did not prove the packaged startup contract.'
}

[pscustomobject]@{
    Status = 'passed'
    Adapters = 6
    PackageSha256 = $newHash
} | ConvertTo-Json -Compress
