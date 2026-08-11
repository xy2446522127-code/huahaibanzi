param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$OldInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedOldSha256,
    [Parameter(Mandatory = $true)][string]$NewInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedNewSha256,
    [Parameter(Mandatory = $true)][string]$FromVersion,
    [Parameter(Mandatory = $true)][string]$ToVersion,
    [Parameter(Mandatory = $true)][string]$Architecture
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'payload_upgrade_component') { throw 'Unexpected evidence ID.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$probeProject = Join-Path $projectRoot 'tools\HuahaiClipboard.UpdateEvidenceProbe\HuahaiClipboard.UpdateEvidenceProbe.csproj'
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OldInstaller).Hash.ToLowerInvariant()
$newHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $NewInstaller).Hash.ToLowerInvariant()
if ($oldHash -ne $ExpectedOldSha256.ToLowerInvariant() -or $newHash -ne $ExpectedNewSha256.ToLowerInvariant()) {
    throw 'Isolated upgrade package SHA-256 mismatch.'
}
$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.IsolatedUpgrade.' + [guid]::NewGuid().ToString('N'))
try {
    $output = & dotnet run --project $probeProject --configuration Release -- upgrade `
        --old-installer $OldInstaller --new-installer $NewInstaller --work-root $workRoot `
        --expected-from $FromVersion --expected-to $ToVersion 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Isolated upgrade probe failed: $($output -join [Environment]::NewLine)" }
    $result = ($output | Select-Object -Last 1) | ConvertFrom-Json
    if ($result.status -ne 'passed' -or -not $result.dataPreserved -or -not $result.startupPayloadReady) {
        throw 'Isolated upgrade did not preserve representative data and a runnable payload.'
    }
    [ordered]@{
        evidence_id = $EvidenceId
        passed = $true
        from_version = $FromVersion
        to_version = $ToVersion
        architecture = $Architecture
        package_sha256 = $newHash
        user_data_preserved = $true
        installed_upgrade_proven = $false
        startup_succeeded = $false
        evidence_scope = 'extracted-payload-transaction-component'
    } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
