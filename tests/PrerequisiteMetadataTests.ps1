$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fetchScript = Join-Path $projectRoot 'installer\Fetch-Prerequisites.ps1'
$prerequisiteRoot = Join-Path $projectRoot 'dist\prerequisites'
$result = & $fetchScript -Destination $prerequisiteRoot | ConvertFrom-Json

if ($result.DotNetVersion -notmatch '^8\.0\.\d+$') {
    throw "Unexpected .NET Desktop Runtime version: $($result.DotNetVersion)"
}
if ($result.DotNetUrl -notmatch '^https://(builds\.dotnet\.microsoft\.com|download\.visualstudio\.microsoft\.com)/') {
    throw "Unexpected .NET download URL: $($result.DotNetUrl)"
}
if ($result.DotNetSha512 -notmatch '^[0-9a-f]{128}$') {
    throw 'The .NET Desktop Runtime SHA-512 is missing or invalid.'
}
$manifestPath = Join-Path $prerequisiteRoot 'prerequisites.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'The prerequisite fetch must write prerequisites.json.'
}

$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
if ($manifest.DotNet.FileName -notmatch '^windowsdesktop-runtime-8\.0\.\d+-win-x64\.exe$' -or
    $manifest.DotNet.Version -notmatch '^8\.0\.\d+$' -or
    $manifest.DotNet.Sha512 -notmatch '^[0-9a-f]{128}$') {
    throw 'The prerequisite manifest has invalid .NET metadata.'
}
if ($null -ne $manifest.WindowsAppRuntime) { throw 'Native WPF setup must not include Windows App Runtime metadata.' }

[pscustomobject]@{
    Status = 'passed'
    DotNetVersion = $result.DotNetVersion
    DotNetUrl = $result.DotNetUrl
} | ConvertTo-Json -Compress
