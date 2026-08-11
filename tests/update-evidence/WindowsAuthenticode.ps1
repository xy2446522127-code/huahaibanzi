param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$ExpectedSha256,
    [Parameter(Mandatory = $true)][string]$PublisherIdentity
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'package_signature') { throw 'Unexpected evidence ID.' }
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $PackagePath).Hash.ToLowerInvariant()
if ($actualHash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'Release package SHA-256 mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $PackagePath
$subject = if ($null -eq $signature.SignerCertificate) { '' } else { $signature.SignerCertificate.Subject }
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or $subject -ne $PublisherIdentity) {
    throw "Release package publisher is not trusted: $subject"
}
[ordered]@{
    evidence_id = $EvidenceId
    passed = $true
    package_sha256 = $actualHash
    authenticity_mode = 'os-package-signature'
    publisher_identity = $PublisherIdentity
    trust_status = 'trusted'
} | ConvertTo-Json -Compress
