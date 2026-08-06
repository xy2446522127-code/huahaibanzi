[CmdletBinding(SupportsShouldProcess)]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'dist'))

if (-not (Test-Path -LiteralPath $distRoot -PathType Container)) {
    [pscustomobject]@{ Removed = 0; Remaining = 0 } | ConvertTo-Json -Compress
    return
}

$targets = @([System.IO.Directory]::EnumerateDirectories(
    $distRoot,
    'installer-integrity-fixture-*',
    [System.IO.SearchOption]::TopDirectoryOnly
))

foreach ($target in $targets) {
    $resolved = [System.IO.Path]::GetFullPath($target)
    $leaf = [System.IO.Path]::GetFileName($resolved)
    $parent = [System.IO.Path]::GetDirectoryName($resolved)

    if (-not $parent.Equals($distRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing fixture outside the project dist directory: $resolved"
    }
    if ($leaf -notmatch '^installer-integrity-fixture-[0-9a-f]{32}$') {
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
    if ($PSCmdlet.ShouldProcess($resolved, 'Remove generated installer integrity fixture')) {
        Remove-Item -LiteralPath $resolved -Recurse -Force
        $removed++
    }
}

$remaining = @([System.IO.Directory]::EnumerateDirectories(
    $distRoot,
    'installer-integrity-fixture-*',
    [System.IO.SearchOption]::TopDirectoryOnly
)).Count

[pscustomobject]@{
    Removed = $removed
    Remaining = $remaining
} | ConvertTo-Json -Compress
