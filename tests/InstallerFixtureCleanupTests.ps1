$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$tempFixtureRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'))
$fixtureParents = @($distRoot, $tempFixtureRoot)
$integrityTest = Join-Path $PSScriptRoot 'InstallerPrerequisiteIntegrityTests.ps1'

function Get-IntegrityFixtureDirectories {
    $directories = @()
    foreach ($parent in $fixtureParents) {
        if (Test-Path -LiteralPath $parent -PathType Container) {
            $directories += @([System.IO.Directory]::EnumerateDirectories(
                $parent,
                'installer-integrity-fixture-*',
                [System.IO.SearchOption]::TopDirectoryOnly
            ) | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
        }
    }

    @($directories)
}

$before = @(Get-IntegrityFixtureDirectories)
$created = @()

try {
    & $integrityTest | Out-Null
    $after = @(Get-IntegrityFixtureDirectories)
    $created = @($after | Where-Object { $_ -notin $before })

    if ($created.Count -ne 0) {
        throw "Installer integrity test left $($created.Count) executable fixture director$(if ($created.Count -eq 1) { 'y' } else { 'ies' }) behind."
    }

    [pscustomobject]@{
        Status = 'passed'
        NewFixtureDirectories = 0
    } | ConvertTo-Json -Compress
}
finally {
    if ($created.Count -eq 0) {
        $created = @((Get-IntegrityFixtureDirectories) | Where-Object { $_ -notin $before })
    }

    foreach ($path in $created) {
        $leaf = Split-Path -Leaf $path
        $parent = [System.IO.Path]::GetDirectoryName($path)
        $isOwnedParent = @($fixtureParents | Where-Object {
            $_.Equals($parent, [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -eq 1
        if ($isOwnedParent -and $leaf -match '^installer-integrity-fixture-[0-9a-f]{32}$') {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}
