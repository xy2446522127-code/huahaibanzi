$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$adapterRoot = Join-Path $PSScriptRoot 'update-evidence'
$newInstaller = if ([string]::IsNullOrWhiteSpace($env:HUAHAI_RELEASE_INSTALLER_FIXTURE)) {
    Join-Path $projectRoot 'dist\HuahaiClipboard-Setup.exe'
} else { [IO.Path]::GetFullPath($env:HUAHAI_RELEASE_INSTALLER_FIXTURE) }
$oldInstaller = if ([string]::IsNullOrWhiteSpace($env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE)) {
    Join-Path $projectRoot 'dist\HuahaiClipboard-Setup-1.1.10.exe'
} else { [IO.Path]::GetFullPath($env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE) }
$missingArtifacts = @($newInstaller, $oldInstaller) | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
if ($missingArtifacts.Count -gt 0) {
    [pscustomobject]@{ Status = 'skipped'; Reason = 'release-artifacts-unavailable'; Missing = $missingArtifacts } |
        ConvertTo-Json -Compress
    exit 0
}
$newHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $newInstaller).Hash.ToLowerInvariant()
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $oldInstaller).Hash.ToLowerInvariant()
$publisher = 'CN=HuahaiClipboard Open Source Release'
$publisherThumbprint = 'CD06B727BD8811C3B59CE0A4F9384D68EC7431C2'

function Invoke-Adapter {
    param([Parameter(Mandatory = $true)][string]$Script, [Parameter(Mandatory = $true)][string[]]$Arguments)
    $output = & powershell.exe -NoProfile -NonInteractive -File (Join-Path $adapterRoot $Script) @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Script failed with exit code $LASTEXITCODE" }
    return $output | ConvertFrom-Json
}

$previousErrorAction = $ErrorActionPreference
try {
    $ErrorActionPreference = 'Continue'
    $wrongPublisherOutput = & powershell.exe -NoProfile -NonInteractive -File `
        (Join-Path $adapterRoot 'WindowsAuthenticode.ps1') `
        package_signature $newInstaller $newHash $publisher ('0' * 40) 2>&1
    $wrongPublisherExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorAction
}
if ($wrongPublisherExitCode -eq 0) { throw 'The Authenticode adapter accepted the wrong publisher thumbprint.' }

$signature = Invoke-Adapter 'WindowsAuthenticode.ps1' @(
    'package_signature', $newInstaller, $newHash, $publisher, $publisherThumbprint)
if (-not $signature.passed -or $signature.trust_status -ne 'trusted' -or
    $signature.publisher_thumbprint -ne $publisherThumbprint) {
    throw 'The Authenticode evidence adapter did not verify the release package.'
}

$integrity = Invoke-Adapter 'UpdateSecurityFixture.ps1' @(
    'integrity_rejection', $newInstaller, $newHash)
if (-not $integrity.passed -or
    (Compare-Object @($integrity.blocked_cases) @(
        'digest_mismatch', 'unsigned', 'wrong_publisher', 'tampered_signature'
    ))) {
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
    'payload_upgrade_component', $oldInstaller, $oldHash, $newInstaller, $newHash, '1.1.10', '1.1.11', 'x64')
if (-not $upgrade.passed -or -not $upgrade.user_data_preserved -or $upgrade.installed_upgrade_proven -or
    $upgrade.startup_succeeded) {
    throw 'The payload upgrade component check overstated installed upgrade or startup evidence.'
}

$startup = Invoke-Adapter 'InstalledStartupSmoke.ps1' @(
    'packaged_payload_readiness', $newInstaller, $newHash, '1.1.11')
if (-not $startup.passed -or $startup.process_started -or $startup.startup_succeeded -or
    @($startup.criteria).Count -lt 2) {
    throw 'The payload readiness check overstated process startup evidence.'
}

[pscustomobject]@{
    Status = 'passed'
    ReleaseEvidenceAdapters = 4
    ComponentChecks = 2
    PackageSha256 = $newHash
} | ConvertTo-Json -Compress
