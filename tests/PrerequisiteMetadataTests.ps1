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
if ($result.WindowsAppRuntimeVersion -notmatch '^1\.7') {
    throw "Unexpected Windows App Runtime version: $($result.WindowsAppRuntimeVersion)"
}
if ($result.WindowsAppRuntimeUrl -notmatch '^https://aka\.ms/windowsappsdk/1\.7/') {
    throw "Unexpected Windows App Runtime download URL: $($result.WindowsAppRuntimeUrl)"
}
if ($result.WebView2RuntimeVersion -ne 'Evergreen' -or
    $result.WebView2RuntimeUrl -notmatch '^https://go\.microsoft\.com/fwlink/\?linkid=2124701$') {
    throw 'Unexpected Evergreen WebView2 Runtime metadata.'
}
$webView2RuntimeFile = Get-Item -LiteralPath $result.WebView2RuntimePath
if ($webView2RuntimeFile.Length -lt 100MB) {
    throw "Evergreen WebView2 prerequisite must be the offline x64 standalone installer, not the small network bootstrapper. Size=$($webView2RuntimeFile.Length)"
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
if ($manifest.WindowsAppRuntime.FileName -ne 'WindowsAppRuntimeInstall-x64.exe' -or
    $manifest.WindowsAppRuntime.Version -notmatch '^1\.7' -or
    $manifest.WindowsAppRuntime.Sha512 -notmatch '^[0-9a-f]{128}$') {
    throw 'The prerequisite manifest has invalid Windows App Runtime metadata.'
}
if ($manifest.WebView2Runtime.FileName -ne 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' -or
    $manifest.WebView2Runtime.Version -ne 'Evergreen' -or
    $manifest.WebView2Runtime.Sha512 -notmatch '^[0-9a-f]{128}$') {
    throw 'The prerequisite manifest has invalid Evergreen WebView2 Runtime metadata.'
}

[pscustomobject]@{
    Status = 'passed'
    DotNetVersion = $result.DotNetVersion
    DotNetUrl = $result.DotNetUrl
    WindowsAppRuntimeVersion = $result.WindowsAppRuntimeVersion
    WindowsAppRuntimeUrl = $result.WindowsAppRuntimeUrl
    WebView2RuntimeVersion = $result.WebView2RuntimeVersion
    WebView2RuntimeUrl = $result.WebView2RuntimeUrl
} | ConvertTo-Json -Compress
