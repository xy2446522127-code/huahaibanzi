$ErrorActionPreference = 'Stop'

$missingRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.MissingReleaseArtifacts.' + [guid]::NewGuid().ToString('N'))
$previousRelease = $env:HUAHAI_RELEASE_INSTALLER_FIXTURE
$previousOld = $env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE

try {
    $env:HUAHAI_RELEASE_INSTALLER_FIXTURE = Join-Path $missingRoot 'new.exe'
    $env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE = Join-Path $missingRoot 'old.exe'
    foreach ($scriptName in @(
        'UpdateEvidenceProbeTests.ps1',
        'UpdateEvidenceAdaptersTests.ps1',
        'ReleasedClientProbeAdapterTests.ps1'
    )) {
        $output = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File `
            (Join-Path $PSScriptRoot $scriptName) 2>&1
        if ($LASTEXITCODE -ne 0) { throw "$scriptName failed instead of reporting unavailable artifacts." }
        $result = ($output | Select-Object -Last 1) | ConvertFrom-Json
        if ($result.Status -ne 'skipped' -or $result.Reason -ne 'release-artifacts-unavailable') {
            throw "$scriptName did not report an explicit release-artifacts-unavailable skip."
        }
    }

    [pscustomobject]@{ Status = 'passed'; Scripts = 3 } | ConvertTo-Json -Compress
}
finally {
    $env:HUAHAI_RELEASE_INSTALLER_FIXTURE = $previousRelease
    $env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE = $previousOld
}
