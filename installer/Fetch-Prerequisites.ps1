param(
    [Parameter(Mandatory = $true)][string]$Destination,
    [switch]$MetadataOnly
)

$ErrorActionPreference = 'Stop'
$destination = [System.IO.Path]::GetFullPath($Destination)
$releaseMetadataUrl = 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json'
$windowsAppRuntimeVersion = '1.7'
$windowsAppRuntimeUrl = 'https://aka.ms/windowsappsdk/1.7/latest/windowsappruntimeinstall-x64.exe'
$webView2RuntimeVersion = 'Evergreen'
$webView2RuntimeUrl = 'https://go.microsoft.com/fwlink/?linkid=2124701'

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
    WindowsAppRuntimeVersion = $windowsAppRuntimeVersion
    WindowsAppRuntimeUrl = $windowsAppRuntimeUrl
    WebView2RuntimeVersion = $webView2RuntimeVersion
    WebView2RuntimeUrl = $webView2RuntimeUrl
}

if ($MetadataOnly) {
    [pscustomobject]$result | ConvertTo-Json -Compress
    exit 0
}

New-Item -ItemType Directory -Path $destination -Force | Out-Null
$dotNetPath = Join-Path $destination ('windowsdesktop-runtime-' + $result.DotNetVersion + '-win-x64.exe')
$windowsAppRuntimePath = Join-Path $destination 'WindowsAppRuntimeInstall-x64.exe'
$webView2RuntimePath = Join-Path $destination 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'

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

function Test-MicrosoftSignature([string]$Path) {
    $signature = Get-AuthenticodeSignature -FilePath $Path
    return $signature.Status -eq [System.Management.Automation.SignatureStatus]::Valid -and
        $null -ne $signature.SignerCertificate -and
        $signature.SignerCertificate.Subject -match 'O=Microsoft Corporation'
}

function Download-VerifiedWindowsAppRuntime([string]$Path) {
    if ((Test-Path -LiteralPath $Path) -and (Test-MicrosoftSignature $Path)) { return }

    $downloadPath = $Path + '.download-' + [guid]::NewGuid().ToString('N')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $windowsAppRuntimeUrl -OutFile $downloadPath
        if (-not (Test-MicrosoftSignature $downloadPath)) {
            throw 'The Windows App Runtime installer does not have a valid Microsoft signature.'
        }
        Move-Item -LiteralPath $downloadPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath -Force }
    }
}

Download-VerifiedWindowsAppRuntime $windowsAppRuntimePath

function Download-VerifiedWebView2Runtime([string]$Path) {
    if ((Test-Path -LiteralPath $Path) -and
        (Get-Item -LiteralPath $Path).Length -ge 100MB -and
        (Test-MicrosoftSignature $Path)) { return }

    $downloadPath = $Path + '.download-' + [guid]::NewGuid().ToString('N')
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $webView2RuntimeUrl -OutFile $downloadPath
        if (-not (Test-MicrosoftSignature $downloadPath)) {
            throw 'The Evergreen WebView2 Runtime installer does not have a valid Microsoft signature.'
        }
        if ((Get-Item -LiteralPath $downloadPath).Length -lt 100MB) {
            throw 'The Evergreen WebView2 download is not the offline x64 standalone installer.'
        }
        Move-Item -LiteralPath $downloadPath -Destination $Path -Force
    }
    finally {
        if (Test-Path -LiteralPath $downloadPath) { Remove-Item -LiteralPath $downloadPath -Force }
    }
}

Download-VerifiedWebView2Runtime $webView2RuntimePath
$manifest = [ordered]@{
    DotNet = [ordered]@{
        FileName = [System.IO.Path]::GetFileName($dotNetPath)
        Version = $result.DotNetVersion
        Sha512 = Get-Sha512 $dotNetPath
    }
    WindowsAppRuntime = [ordered]@{
        FileName = [System.IO.Path]::GetFileName($windowsAppRuntimePath)
        Version = $windowsAppRuntimeVersion
        Sha512 = Get-Sha512 $windowsAppRuntimePath
    }
    WebView2Runtime = [ordered]@{
        FileName = [System.IO.Path]::GetFileName($webView2RuntimePath)
        Version = $webView2RuntimeVersion
        Sha512 = Get-Sha512 $webView2RuntimePath
    }
}
$manifestPath = Join-Path $destination 'prerequisites.json'
$manifestJson = $manifest | ConvertTo-Json -Depth 4
[System.IO.File]::WriteAllText($manifestPath, $manifestJson, (New-Object System.Text.UTF8Encoding($false)))

$result.DotNetPath = $dotNetPath
$result.DotNetSha512Actual = Get-Sha512 $dotNetPath
$result.WindowsAppRuntimePath = $windowsAppRuntimePath
$result.WindowsAppRuntimeSha512Actual = Get-Sha512 $windowsAppRuntimePath
$result.WebView2RuntimePath = $webView2RuntimePath
$result.WebView2RuntimeSha512Actual = Get-Sha512 $webView2RuntimePath
$result.ManifestPath = $manifestPath
[pscustomobject]$result | ConvertTo-Json -Compress
