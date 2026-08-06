$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$tempFixtureRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'))
$fixtureParents = @($distRoot, $tempFixtureRoot)
$integrityTest = Join-Path $PSScriptRoot 'InstallerPrerequisiteIntegrityTests.ps1'
$packagePolicyTest = Join-Path $PSScriptRoot 'InstallerPackagePolicyTests.ps1'
$cleanupScript = Join-Path $PSScriptRoot 'Remove-InstallerIntegrityFixtures.ps1'
$cleanupProbeRoot = Join-Path $tempFixtureRoot ('fixture-cleanup-probe-' + [guid]::NewGuid().ToString('N'))
$fixturePatterns = @(
    'installer-integrity-fixture-*'
    'installer-policy-fixture-*'
)

function Get-IntegrityFixtureDirectories {
    $directories = @()
    foreach ($parent in $fixtureParents) {
        if (Test-Path -LiteralPath $parent -PathType Container) {
            foreach ($pattern in $fixturePatterns) {
                $directories += @([System.IO.Directory]::EnumerateDirectories(
                    $parent,
                    $pattern,
                    [System.IO.SearchOption]::TopDirectoryOnly
                ) | ForEach-Object { [System.IO.Path]::GetFullPath($_) })
            }
        }
    }

    @($directories)
}

$before = @(Get-IntegrityFixtureDirectories)
$created = @()

try {
    & $integrityTest | Out-Null
    & $packagePolicyTest | Out-Null
    $after = @(Get-IntegrityFixtureDirectories)
    $created = @($after | Where-Object { $_ -notin $before })

    if ($created.Count -ne 0) {
        throw "Installer tests left $($created.Count) executable fixture director$(if ($created.Count -eq 1) { 'y' } else { 'ies' }) behind."
    }

    New-Item -ItemType Directory -Path (Join-Path $cleanupProbeRoot ('installer-integrity-fixture-' + ('1' * 32))) -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $cleanupProbeRoot ('installer-policy-fixture-' + ('2' * 32))) -Force | Out-Null
    $cleanup = & $cleanupScript -DistRoot $cleanupProbeRoot | ConvertFrom-Json
    if ($cleanup.Removed -ne 2 -or $cleanup.Remaining -ne 0) {
        throw "Installer fixture cleanup did not remove both supported fixture families."
    }

    [pscustomobject]@{
        Status = 'passed'
        NewFixtureDirectories = 0
        CleanupFamilies = 2
    } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $cleanupProbeRoot) {
        Remove-Item -LiteralPath $cleanupProbeRoot -Recurse -Force
    }

    if ($created.Count -eq 0) {
        $created = @((Get-IntegrityFixtureDirectories) | Where-Object { $_ -notin $before })
    }

    foreach ($path in $created) {
        $leaf = Split-Path -Leaf $path
        $parent = [System.IO.Path]::GetDirectoryName($path)
        $isOwnedParent = @($fixtureParents | Where-Object {
            $_.Equals($parent, [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -eq 1
        if ($isOwnedParent -and $leaf -match '^installer-(?:integrity|policy)-fixture-[0-9a-f]{32}$') {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}
