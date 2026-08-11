$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$probeProject = Join-Path $projectRoot 'tools\HuahaiClipboard.UpdateEvidenceProbe\HuahaiClipboard.UpdateEvidenceProbe.csproj'
$installer = Join-Path $projectRoot 'dist\HuahaiClipboard-Setup.exe'
$oldInstaller = Join-Path $projectRoot 'dist\HuahaiClipboard-Setup-1.1.10.exe'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.UpdateEvidenceProbe.' + [guid]::NewGuid().ToString('N'))
$extracted = Join-Path $fixtureRoot 'extracted'
$upgradeRoot = Join-Path $fixtureRoot 'upgrade'

try {
    if (-not (Test-Path -LiteralPath $installer)) { throw "Installer fixture is missing: $installer" }
    if (-not (Test-Path -LiteralPath $oldInstaller)) { throw "Old installer fixture is missing: $oldInstaller" }

    $json = & dotnet run --project $probeProject --configuration Release -- extract `
        --installer $installer `
        --destination $extracted
    if ($LASTEXITCODE -ne 0) { throw "Update evidence probe failed with exit code $LASTEXITCODE" }

    $result = $json | ConvertFrom-Json
    if ($result.status -ne 'passed') { throw 'Update evidence extraction did not pass.' }
    if (-not (Test-Path -LiteralPath (Join-Path $extracted 'HuahaiClipboard.App.exe'))) {
        throw 'Update evidence extraction omitted the application executable.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $extracted 'Assets\Web\product-shell.html'))) {
        throw 'Update evidence extraction omitted the approved Web shell.'
    }

    $upgradeJson = & dotnet run --project $probeProject --configuration Release --no-build -- upgrade `
        --old-installer $oldInstaller `
        --new-installer $installer `
        --work-root $upgradeRoot `
        --expected-from 1.1.10 `
        --expected-to 1.1.11
    if ($LASTEXITCODE -ne 0) { throw "Update evidence upgrade failed with exit code $LASTEXITCODE" }
    $upgrade = $upgradeJson | ConvertFrom-Json
    if ($upgrade.status -ne 'passed' -or -not $upgrade.dataPreserved -or -not $upgrade.startupPayloadReady) {
        throw 'The isolated upgrade did not preserve data and a runnable payload.'
    }

    $discoveryJson = & dotnet run --project $probeProject --configuration Release --no-build -- probe-update `
        --core (Join-Path $extracted 'HuahaiClipboard.Core.dll') `
        --current 1.1.9 `
        --expected-target 1.1.10
    if ($LASTEXITCODE -ne 0) { throw "Released update component probe failed with exit code $LASTEXITCODE" }
    $discovery = $discoveryJson | ConvertFrom-Json
    if ($discovery.status -ne 'passed' -or -not $discovery.updateAvailable -or $discovery.latestVersion -ne '1.1.10') {
        throw 'The released update component did not discover the public latest release.'
    }

    [pscustomobject]@{
        Status = 'passed'
        ExtractedVersion = $result.version
        FileCount = $result.fileCount
        UpgradeFrom = $upgrade.fromVersion
        UpgradeTo = $upgrade.toVersion
        DiscoveredVersion = $discovery.latestVersion
    } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
