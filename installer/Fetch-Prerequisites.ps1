param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$MetadataOnly
)

$ErrorActionPreference = 'Stop'
$destination = [System.IO.Path]::GetFullPath($Destination)
$releaseMetadataUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json'

$metadata = Invoke-RestMethod -UseBasicParsing -Uri $releaseMetadataUrl
$latestVersion = [string]$metadata.'latest-release'
$release = $metadata.releases | Where-Object { $_.'release-version' -eq $latestVersion } | Select-Object -First 1
if ($null -eq $release) { throw "The .NET 8 release metadata does not contain $latestVersion." }

$desktopRuntime = $release.windowsdesktop.files |
    Where-Object { $_.rid -eq 'win-x64' -and $_.name -eq 'windowsdesktop-runtime-win-x64.exe' } |
    Select-Object -First 1
if ($null -eq $desktopRuntime) { throw 'The .NET 8 release metadata does not contain the x64 Windows Desktop Runtime.' }

$result = [ordered]@{
    DotNetVersion = [string]$release.windowsdesktop.version
    DotNetUrl = [string]$desktopRuntime.url
    DotNetSha512 = ([string]$desktopRuntime.hash).ToLowerInvariant()
}

if ($MetadataOnly) {
    [pscustomobject]$result | ConvertTo-Json -Compress
    exit 0
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
$dotNetPath = Join-Path $destination ('windowsdesktop-runtime-' + $result.DotNetVersion + '-win-x64.exe')

# 计算下载文件的 SHA-512，用于核对微软发布元数据。
function Get-Sha512([string]$Path) {
    return (Get-FileHash -Algorithm SHA512 -LiteralPath $Path).Hash.ToLowerInvariant()
}

# 下载并校验 .NET Desktop Runtime，已有正确文件时直接复用。
function Download-VerifiedDotNetRuntime([string]$Path) {
    if ((Test-Path -LiteralPath $Path) -and (Get-Sha512 $Path) -eq $result.DotNetSha512) { return }

    $downloadPath = $Path + '.download-' + [guid]::NewGuid().ToString('N')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $result.DotNetUrl -OutFile $downloadPath
        $actualHash = Get-Sha512 $downloadPath
        if ($actualHash -ne $result.DotNetSha512) {
            throw "The .NET Desktop Runtime SHA-512 does not match. Expected $($result.DotNetSha512), actual $actualHash."
        }
        Move-Item -LiteralPath $downloadPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath -Force }
    }
}

Download-VerifiedDotNetRuntime $dotNetPath
$manifest = [ordered]@{
    DotNet = [ordered]@{
        FileName = [System.IO.Path]::GetFileName($dotNetPath)
        Version = $result.DotNetVersion
        Sha512 = Get-Sha512 $dotNetPath
    }
}
$manifestPath = Join-Path $destination 'prerequisites.json'
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, (New-Object System.Text.UTF8Encoding($false)))

$result.DotNetPath = $dotNetPath
$result.DotNetSha512Actual = Get-Sha512 $dotNetPath
$result.ManifestPath = $manifestPath
[pscustomobject]$result | ConvertTo-Json -Compress
