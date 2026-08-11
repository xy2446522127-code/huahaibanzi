param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ExpectedSha256
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'integrity_rejection') { throw 'Unexpected evidence ID.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath).Hash.ToLowerInvariant()
if ($actualHash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'Release package SHA-256 mismatch.' }
$updateTests = & dotnet test (Join-Path $projectRoot 'tests\HuahaiClipboard.Core.Tests\HuahaiClipboard.Core.Tests.csproj') `
    --configuration Release --no-restore --filter 'FullyQualifiedName~GitHubUpdateCheckServiceTests' --verbosity quiet 2>&1
if ($LASTEXITCODE -ne 0) { throw "Update security tests failed: $($updateTests -join [Environment]::NewLine)" }
$signatureTests = & powershell.exe -NoProfile -NonInteractive -File (Join-Path $projectRoot 'tests\InstallerPublisherSignatureTests.ps1') 2>&1
if ($LASTEXITCODE -ne 0) { throw "Publisher rejection tests failed: $($signatureTests -join [Environment]::NewLine)" }
[ordered]@{
    evidence_id = $EvidenceId
    passed = $true
    package_sha256 = $actualHash
    blocked_cases = @('digest_mismatch', 'unsigned', 'wrong_publisher', 'wrong_host')
} | ConvertTo-Json -Compress
