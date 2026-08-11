param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$TransactionAdapter,
    [Parameter(Mandatory = $true)][string]$ActivePathIdentity,
    [Parameter(Mandatory = $true)][string]$StagingPathIdentity,
    [Parameter(Mandatory = $true)][string]$BackupPathIdentity
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'transaction_rollback') { throw 'Unexpected evidence ID.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$output = & powershell.exe -NoProfile -NonInteractive -File (Join-Path $projectRoot 'tests\InstallerSwapTransactionTests.ps1') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Installer rollback tests failed: $($output -join [Environment]::NewLine)" }
$result = ($output | Select-Object -Last 1) | ConvertFrom-Json
if ($result.Status -ne 'passed' -or [int]$result.Scenarios -lt 4) { throw 'Installer rollback scenarios are incomplete.' }
[ordered]@{
    evidence_id = $EvidenceId
    passed = $true
    transaction_adapter = $TransactionAdapter
    active_path_identity = $ActivePathIdentity
    staging_path_identity = $StagingPathIdentity
    backup_path_identity = $BackupPathIdentity
    scenarios = @('activation_failure', 'locked_candidate', 'cleanup_failure')
} | ConvertTo-Json -Compress
