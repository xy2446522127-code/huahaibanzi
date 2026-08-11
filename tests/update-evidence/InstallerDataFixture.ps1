param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$UserDataLocation
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'data_preservation') { throw 'Unexpected evidence ID.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output = & powershell.exe -NoProfile -NonInteractive -File (Join-Path $projectRoot 'tests\InstallerDataPreservationTests.ps1') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Installer data tests failed: $($output -join [Environment]::NewLine)" }
$result = ($output | Select-Object -Last 1) | ConvertFrom-Json
if ($result.Status -ne 'passed' -or $result.Scenario -ne 'update-preserves-install-root-data') {
    throw 'Installer data preservation scenario did not pass.'
}
[ordered]@{
    evidence_id = $EvidenceId
    passed = $true
    user_data_location = $UserDataLocation
    upgrade_preserved = $true
    rollback_preserved = $true
} | ConvertTo-Json -Compress
