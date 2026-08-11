$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$adapter = Join-Path $PSScriptRoot 'update-evidence\ReleasedClientProbe.ps1'
$oldInstaller = if ([string]::IsNullOrWhiteSpace($env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE)) {
    Join-Path $projectRoot 'dist\HuahaiClipboard-Setup-1.1.10.exe'
} else { [IO.Path]::GetFullPath($env:HUAHAI_PREVIOUS_INSTALLER_FIXTURE) }
if (-not (Test-Path -LiteralPath $oldInstaller -PathType Leaf)) {
    [pscustomobject]@{ Status = 'skipped'; Reason = 'release-artifacts-unavailable'; Missing = @($oldInstaller) } |
        ConvertTo-Json -Compress
    exit 0
}
$oldHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $oldInstaller).Hash.ToLowerInvariant()

$output = & powershell.exe -NoProfile -NonInteractive -File $adapter `
    discovery $oldInstaller $oldHash 1.1.10 1.1.11 stable
if ($LASTEXITCODE -ne 0) { throw "Released client probe failed with exit code $LASTEXITCODE" }
$result = $output | ConvertFrom-Json
if (-not $result.passed -or -not $result.update_available -or $result.target_version -ne '1.1.11') {
    throw 'The released v1.1.10 client component did not discover v1.1.11.'
}

[pscustomobject]@{ Status = 'passed'; From = '1.1.10'; To = '1.1.11' } | ConvertTo-Json -Compress
