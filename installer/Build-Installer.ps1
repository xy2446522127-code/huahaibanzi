param(
    [Parameter(Mandatory = $true)][string]$PublishRoot,
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$PrerequisiteRoot
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
    'HuahaiClipboard.NativeUiSpike.exe',
    'HuahaiClipboard.NativeUiSpike.dll',
    'HuahaiClipboard.NativeUiSpike.deps.json',
    'HuahaiClipboard.NativeUiSpike.runtimeconfig.json',
    'HuahaiClipboard.Core.dll',
    'Microsoft.Windows.SDK.NET.dll',
    'WinRT.Runtime.dll'
)
foreach ($relativePath in $requiredReleaseFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $publishRoot $relativePath))) {
        throw "Release directory is incomplete: $relativePath"
    }
}
if (-not (Test-Path -LiteralPath $csc)) { throw "C# compiler was not found: $csc" }
if (-not (Test-Path -LiteralPath $referenceRoot)) { throw ".NET Framework 4.8 reference assemblies were not found: $referenceRoot" }
$dotNetInstallers = @(Get-ChildItem -LiteralPath $prerequisiteRoot -Filter 'windowsdesktop-runtime-8.*-win-x64.exe' -File -ErrorAction SilentlyContinue)
$prerequisiteManifestPath = Join-Path $prerequisiteRoot 'prerequisites.json'
if ($dotNetInstallers.Count -ne 1) { throw 'Prerequisite directory must contain exactly one .NET 8 x64 Windows Desktop Runtime installer.' }
if (-not (Test-Path -LiteralPath $prerequisiteManifestPath)) { throw 'Prerequisite directory is missing prerequisites.json.' }

# 构建前重新验证下载物，避免仅凭文件名接受被替换的安装器。
$prerequisiteManifest = Get-Content -Raw -LiteralPath $prerequisiteManifestPath | ConvertFrom-Json
if ($prerequisiteManifest.DotNet.FileName -ne $dotNetInstallers[0].Name) {
    throw 'The .NET prerequisite file name does not match prerequisites.json.'
}
$dotNetHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $dotNetInstallers[0].FullName).Hash.ToLowerInvariant()
if ($dotNetHash -ne ([string]$prerequisiteManifest.DotNet.Sha512).ToLowerInvariant()) {
    throw 'The .NET prerequisite SHA-512 does not match prerequisites.json.'
}

try {
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $publishRoot -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $payloadRoot -Recurse -Force
    }

    Rename-Item -LiteralPath (Join-Path $payloadRoot 'HuahaiClipboard.NativeUiSpike.exe') -NewName 'HuahaiClipboard.exe'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall.ps1') -Destination (Join-Path $payloadRoot 'Uninstall.ps1') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UninstallPolicy.ps1') -Destination (Join-Path $payloadRoot 'UninstallPolicy.ps1') -Force
    $payloadPrerequisites = Join-Path $payloadRoot 'prerequisites'
    New-Item -ItemType Directory -Path $payloadPrerequisites -Force | Out-Null
    Copy-Item -LiteralPath $dotNetInstallers[0].FullName -Destination $payloadPrerequisites -Force

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
        (Join-Path $PSScriptRoot 'Bootstrapper.cs')
    )

    & $csc @compilerArgs
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath)) {
        throw "Installer compilation failed with exit code $LASTEXITCODE"
    }

    Get-Item -LiteralPath $outputPath
}
finally {
    if (Test-Path -LiteralPath $buildRoot) {
        Remove-Item -LiteralPath $buildRoot -Recurse -Force
    }
}
