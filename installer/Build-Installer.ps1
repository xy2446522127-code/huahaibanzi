param(
    [Parameter(Mandatory = $true)][string]$PublishRoot,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$PrerequisiteRoot,
    [string]$SigningThumbprint
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$publishRoot = [System.IO.Path]::GetFullPath($PublishRoot)
$outputPath = [System.IO.Path]::GetFullPath($OutputPath)
if ([string]::IsNullOrWhiteSpace($PrerequisiteRoot)) {
    $PrerequisiteRoot = Join-Path $projectRoot 'dist\prerequisites'
}
$prerequisiteRoot = [System.IO.Path]::GetFullPath($PrerequisiteRoot)
$buildRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboardInstaller-' + [guid]::NewGuid().ToString('N'))
$payloadRoot = Join-Path $buildRoot 'payload'
$payloadZip = Join-Path $buildRoot 'payload.zip'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$referenceRoot = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8'

$requiredReleaseFiles = @(
    'HuahaiClipboard.App.exe',
    'HuahaiClipboard.App.dll',
    'HuahaiClipboard.App.deps.json',
    'HuahaiClipboard.App.runtimeconfig.json',
    'HuahaiClipboard.App.pri',
    'HuahaiClipboard.Core.dll',
    'Microsoft.WinUI.dll',
    'Microsoft.Web.WebView2.Core.dll',
    'Microsoft.WindowsAppRuntime.Bootstrap.Net.dll',
    'Microsoft.Windows.SDK.NET.dll',
    'WinRT.Runtime.dll',
    'App.xbf',
    'Presentation\Windows\CursorPanelWindow.xbf',
    'Assets\Web\product-shell.html',
    'Assets\Web\panel-scale.js',
    'Assets\Web\virtual-record-list.js',
    'WebView2Loader.dll',
    'Microsoft.WindowsAppRuntime.Bootstrap.dll'
)
foreach ($relativePath in $requiredReleaseFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $relativePath))) {
        throw "Release directory is incomplete: $relativePath"
    }
}
if (-not (Test-Path -LiteralPath $csc)) { throw "C# compiler was not found: $csc" }
if (-not (Test-Path -LiteralPath $referenceRoot)) { throw ".NET Framework 4.8 reference assemblies were not found: $referenceRoot" }
$dotNetInstallers = @(Get-ChildItem -LiteralPath $prerequisiteRoot -Filter 'windowsdesktop-runtime-8.*-win-x64.exe' -File -ErrorAction SilentlyContinue)
$webView2RuntimeInstallers = @(Get-ChildItem -LiteralPath $prerequisiteRoot -Filter 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe' -File -ErrorAction SilentlyContinue)
$prerequisiteManifestPath = Join-Path $prerequisiteRoot 'prerequisites.json'
if ($dotNetInstallers.Count -ne 1) { throw 'Prerequisite directory must contain exactly one .NET 8 x64 Windows Desktop Runtime installer.' }
if ($webView2RuntimeInstallers.Count -ne 1) { throw 'Prerequisite directory must contain exactly one Evergreen WebView2 Runtime x64 installer.' }
if (-not (Test-Path -LiteralPath $prerequisiteManifestPath)) { throw 'Prerequisite directory is missing prerequisites.json.' }

# 构建前重新验证下载物，避免仅凭文件名接受被替换的安装器。
$prerequisiteManifest = Get-Content -Raw -LiteralPath $prerequisiteManifestPath | ConvertFrom-Json
function Assert-MicrosoftAuthenticodeSignature {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$DisplayName
    )

    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    $subject = if ($null -eq $signature.SignerCertificate) { '' } else { $signature.SignerCertificate.Subject }
    $isMicrosoftPublisher = $subject -match '(?:^|,\s*)O=Microsoft Corporation(?:,|$)'
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or -not $isMicrosoftPublisher) {
        throw "$DisplayName prerequisite does not have a valid Microsoft Authenticode signature."
    }
}

if ($prerequisiteManifest.DotNet.FileName -ne $dotNetInstallers[0].Name) {
    throw 'The .NET prerequisite file name does not match prerequisites.json.'
}
$dotNetHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $dotNetInstallers[0].FullName).Hash.ToLowerInvariant()
if ($dotNetHash -ne ([string]$prerequisiteManifest.DotNet.Sha512).ToLowerInvariant()) {
    throw 'The .NET prerequisite SHA-512 does not match prerequisites.json.'
}
if ($prerequisiteManifest.WebView2Runtime.FileName -ne $webView2RuntimeInstallers[0].Name) {
    throw 'The Evergreen WebView2 Runtime prerequisite file name does not match prerequisites.json.'
}
$webView2RuntimeHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $webView2RuntimeInstallers[0].FullName).Hash.ToLowerInvariant()
if ($webView2RuntimeHash -ne ([string]$prerequisiteManifest.WebView2Runtime.Sha512).ToLowerInvariant()) {
    throw 'The Evergreen WebView2 Runtime prerequisite SHA-512 does not match prerequisites.json.'
}

Assert-MicrosoftAuthenticodeSignature -Path $dotNetInstallers[0].FullName -DisplayName '.NET'
Assert-MicrosoftAuthenticodeSignature -Path $webView2RuntimeInstallers[0].FullName -DisplayName 'Evergreen WebView2 Runtime'

try {
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
    $excludedPayloadNames = @(
        '.huahai-install-owner',
        'Data',
        'prerequisites',
        'Uninstall.ps1',
        'UninstallPolicy.ps1'
    )
    Get-ChildItem -LiteralPath $publishRoot -Force | ForEach-Object {
        if ($excludedPayloadNames -notcontains $_.Name -and $_.Name -notlike '*.WebView2') {
            Copy-Item -LiteralPath $_.FullName -Destination $payloadRoot -Recurse -Force
        }
    }

    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $payloadRoot 'Uninstall.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UninstallPolicy.ps1') -Destination (Join-Path $payloadRoot 'UninstallPolicy.ps1') -Force
    $launcherOutput = Join-Path $payloadRoot 'HuahaiClipboard.Launcher.exe'
    & (Join-Path $projectRoot 'launcher\Build-Launcher.ps1') -OutputPath $launcherOutput | Out-Null
    $payloadPrerequisites = Join-Path $payloadRoot 'prerequisites'
    New-Item -ItemType Directory -Path $payloadPrerequisites -Force | Out-Null
    Copy-Item -LiteralPath $dotNetInstallers[0].FullName -Destination $payloadPrerequisites -Force
    Copy-Item -LiteralPath $webView2RuntimeInstallers[0].FullName -Destination $payloadPrerequisites -Force

    Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal
    $outputDirectory = Split-Path -Parent $outputPath
    New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

    $references = @(
        'System.IO.Compression.dll',
        'System.IO.Compression.FileSystem.dll'
    ) | ForEach-Object { '/reference:' + (Join-Path $referenceRoot $_) }

    $compilerArgs = @(
        '/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/debug-',
        ('/out:' + $outputPath),
        ('/win32icon:' + (Join-Path $projectRoot 'src\HuahaiClipboard.App\Assets\Brand\fox-icon.ico')),
        ('/win32manifest:' + (Join-Path $PSScriptRoot 'Bootstrapper.manifest')),
        ('/resource:' + $payloadZip + ',HuahaiClipboard.Payload')
    ) + $references + @(
        (Join-Path $PSScriptRoot 'PrerequisitePolicy.cs'),
        (Join-Path $PSScriptRoot 'InstallLocationPolicy.cs'),
        (Join-Path $PSScriptRoot 'BootstrapperInstallPathPolicy.cs'),
        (Join-Path $PSScriptRoot 'DataLocationPolicy.cs'),
        (Join-Path $PSScriptRoot 'InstallTargetPolicy.cs'),
        (Join-Path $PSScriptRoot 'InstallDataPreserver.cs'),
        (Join-Path $PSScriptRoot 'PostInstallLaunchPolicy.cs'),
        (Join-Path $PSScriptRoot 'InstallerLogPolicy.cs'),
        (Join-Path $PSScriptRoot 'InstallSwapTransaction.cs'),
        (Join-Path $PSScriptRoot 'Bootstrapper.cs')
    )

    & $csc @compilerArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
        throw "Installer compilation failed with exit code $LASTEXITCODE"
    }

    if (-not [string]::IsNullOrWhiteSpace($SigningThumbprint)) {
        & (Join-Path $PSScriptRoot 'Sign-ReleaseInstaller.ps1') `
            -Path $outputPath `
            -Thumbprint $SigningThumbprint | Out-Null
    }

    Get-Item -LiteralPath $outputPath
}
finally {
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
}
