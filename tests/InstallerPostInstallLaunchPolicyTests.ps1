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
if ([PostInstallLaunchPolicy]::ShouldLaunch($false, $true)) {
    throw 'A prerequisite exit code 3010 must prevent immediate application launch.'
}
if ([PostInstallLaunchPolicy]::ShouldLaunch($true, $false)) {
    throw 'The explicit no-launch option must be respected.'
}
if (-not [PostInstallLaunchPolicy]::ShouldLaunch($false, $false)) {
    throw 'A verified installation without a restart requirement must launch normally.'
}

[pscustomobject]@{ Status = 'passed'; Silent = '--background'; Interactive = $null } |
    ConvertTo-Json -Compress
