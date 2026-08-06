[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$DistRoot
)

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectDistRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))
$tempFixtureRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'))
$distRoot = if ([string]::IsNullOrWhiteSpace($DistRoot)) {
    $projectDistRoot
}
else {
    [System.IO.Path]::GetFullPath($DistRoot)
}

$distLeaf = [System.IO.Path]::GetFileName($distRoot)
$distParent = [System.IO.Path]::GetDirectoryName($distRoot)
$isProjectDist = $distRoot.Equals($projectDistRoot, [System.StringComparison]::OrdinalIgnoreCase)
$isTestProbe = $distParent.Equals($tempFixtureRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
    $distLeaf -match '^fixture-cleanup-probe-[0-9a-f]{32}$'
if (-not $isProjectDist -and -not $isTestProbe) {
    throw "Refusing fixture cleanup outside the project dist directory or an owned test probe: $distRoot"
}

if (-not (Test-Path -LiteralPath $distRoot -PathType Container)) {
    [pscustomobject]@{ Removed = 0; Remaining = 0 } | ConvertTo-Json -Compress
    return
}

$targets = @(
    foreach ($pattern in @('installer-integrity-fixture-*', 'installer-policy-fixture-*')) {
        [System.IO.Directory]::EnumerateDirectories(
            $distRoot,
            $pattern,
            [System.IO.SearchOption]::TopDirectoryOnly
        )
    }
)

foreach ($target in $targets) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    $leaf = [System.IO.Path]::GetFileName($resolved)
    $parent = [System.IO.Path]::GetDirectoryName($resolved)

    if (-not $parent.Equals($distRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing fixture outside the project dist directory: $resolved"
    }
    if ($leaf -notmatch '^installer-(?:integrity|policy)-fixture-[0-9a-f]{32}$') {
        throw "Refusing unexpected fixture directory name: $resolved"
    }

    $reparsePoints = @(Get-ChildItem -LiteralPath $resolved -Force -Recurse | Where-Object {
        ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
    })
    if ($reparsePoints.Count -ne 0) {
        throw "Refusing fixture directory containing a reparse point: $resolved"
    }
}

$removed = 0
foreach ($target in $targets) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    if ($PSCmdlet.ShouldProcess($resolved, 'Remove generated installer fixture')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
        $removed++
    }
}

$remaining = @(
    foreach ($pattern in @('installer-integrity-fixture-*', 'installer-policy-fixture-*')) {
        [System.IO.Directory]::EnumerateDirectories(
            $distRoot,
            $pattern,
            [System.IO.SearchOption]::TopDirectoryOnly
        )
    }
).Count

[pscustomobject]@{
    Removed = $removed
    Remaining = $remaining
} | ConvertTo-Json -Compress
