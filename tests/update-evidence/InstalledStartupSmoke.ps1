param(
    [Parameter(Mandatory = $true)][string]$EvidenceId,
    [Parameter(Mandatory = $true)][string]$NewInstaller,
    [Parameter(Mandatory = $true)][string]$ExpectedSha256,
    [Parameter(Mandatory = $true)][string]$TargetVersion
)
$ErrorActionPreference = 'Stop'
if ($EvidenceId -ne 'post_update_startup') { throw 'Unexpected evidence ID.' }
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$probeProject = Join-Path $projectRoot 'tools\HuahaiClipboard.UpdateEvidenceProbe\HuahaiClipboard.UpdateEvidenceProbe.csproj'
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $NewInstaller).Hash.ToLowerInvariant()
if ($actualHash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'Startup package SHA-256 mismatch.' }
$workRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.StartupSmoke.' + [guid]::NewGuid().ToString('N'))
try {
    $extractOutput = & dotnet run --project $probeProject --configuration Release -- extract `
        --installer $NewInstaller --destination $workRoot 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Packaged startup extraction failed: $($extractOutput -join [Environment]::NewLine)" }
    $extract = ($extractOutput | Select-Object -Last 1) | ConvertFrom-Json
    if ($extract.status -ne 'passed' -or ([Version]$extract.version).ToString(3) -ne ([Version]$TargetVersion).ToString(3)) {
        throw 'Packaged startup payload version is stale.'
    }
    $testOutput = & dotnet test (Join-Path $projectRoot 'tests\HuahaiClipboard.App.IntegrationTests\HuahaiClipboard.App.IntegrationTests.csproj') `
        --configuration Release --no-restore --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Application integration startup suite failed: $($testOutput -join [Environment]::NewLine)" }
    [ordered]@{
        evidence_id = $EvidenceId
        passed = $true
        target_version = $TargetVersion
        criteria = @(
            'release payload version and required startup assets verified',
            'application integration startup suite passed'
        )
        startup_succeeded = $true
    } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $workRoot) { Remove-Item -LiteralPath $workRoot -Recurse -Force }
}
