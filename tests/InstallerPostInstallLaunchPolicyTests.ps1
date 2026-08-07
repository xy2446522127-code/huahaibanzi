$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'installer\PostInstallLaunchPolicy.cs'
if (-not (Test-Path -LiteralPath $policySource -PathType Leaf)) {
    throw "Missing post-install launch policy: $policySource"
}

Add-Type -TypeDefinition (Get-Content -LiteralPath $policySource -Raw -Encoding utf8)

if ([PostInstallLaunchPolicy]::ArgumentsFor($true) -ne '--background') {
    throw 'Silent update installs must restart the application in background mode.'
}
if ($null -ne [PostInstallLaunchPolicy]::ArgumentsFor($false)) {
    throw 'Interactive installs must open the application normally.'
}

[pscustomobject]@{ Status = 'passed'; Silent = '--background'; Interactive = $null } |
    ConvertTo-Json -Compress
