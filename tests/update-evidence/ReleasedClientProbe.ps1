param(
    [Parameter(Mandatory = $true)][ValidateSet('discovery', 'live_old_client_probe')][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$OldInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedOldSha256,
    [Parameter(Mandatory = $true)][string]$CurrentVersion,
    [Parameter(Mandatory = $true)][string]$TargetVersion,
    [Parameter(Mandatory = $true)][string]$Channel
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$probeProject = Join-Path $projectRoot 'tools\HuahaiClipboard.UpdateEvidenceProbe\HuahaiClipboard.UpdateEvidenceProbe.csproj'
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $OldInstaller).Hash.ToLowerInvariant()
if ($oldHash -ne $ExpectedOldSha256.ToLowerInvariant()) { throw 'Released old-client package SHA-256 mismatch.' }
$signature = Get-AuthenticodeSignature -LiteralPath $OldInstaller
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw 'Released old-client package signature is not valid.'
}
$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.ReleasedClient.' + [guid]::NewGuid().ToString('N'))
try {
    $extractOutput = & dotnet run --project $probeProject --configuration Release -- extract `
        --installer $OldInstaller --destination $workRoot 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Old-client extraction failed: $($extractOutput -join [Environment]::NewLine)" }
    $extract = ($extractOutput | Select-Object -Last 1) | ConvertFrom-Json
    if (($extract.version -as [Version]).ToString(3) -ne ([Version]$CurrentVersion).ToString(3)) {
        throw 'Released old-client package version does not match the declared current version.'
    }

    $probeOutput = & dotnet run --project $probeProject --configuration Release --no-build -- probe-update `
        --core (Join-Path $workRoot 'HuahaiClipboard.Core.dll') `
        --current $CurrentVersion --expected-target $TargetVersion 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Released update component probe failed: $($probeOutput -join [Environment]::NewLine)" }
    $probe = ($probeOutput | Select-Object -Last 1) | ConvertFrom-Json
    if ($probe.status -ne 'passed' -or -not $probe.updateAvailable -or $probe.latestVersion -ne $TargetVersion) {
        throw 'Released old-client update component did not discover the target version.'
    }

    if ($EvidenceId -eq 'discovery') {
        [ordered]@{
            evidence_id = $EvidenceId
            passed = $true
            current_version = $CurrentVersion
            target_version = $TargetVersion
            channel = $Channel
            update_available = $true
        } | ConvertTo-Json -Compress
    }
    else {
        [ordered]@{
            evidence_id = $EvidenceId
            passed = $true
            old_version = $CurrentVersion
            target_version = $TargetVersion
            client_identity = 'released-installed-binary'
            update_available = $true
        } | ConvertTo-Json -Compress
    }
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
