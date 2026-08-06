$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureParent = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'))
$fixtureRoot = Join-Path $fixtureParent ('installer-integrity-fixture-' + [guid]::NewGuid().ToString('N'))
$dotNetReleaseRoot = Join-Path $fixtureRoot 'dotnet-release'
$windowsReleaseRoot = Join-Path $fixtureRoot 'windows-release'
$webViewReleaseRoot = Join-Path $fixtureRoot 'webview-release'
$dotNetPrerequisiteRoot = Join-Path $fixtureRoot 'dotnet-prerequisites'
$windowsPrerequisiteRoot = Join-Path $fixtureRoot 'windows-prerequisites'
$webViewPrerequisiteRoot = Join-Path $fixtureRoot 'webview-prerequisites'
$probeOutput = Join-Path $fixtureRoot 'probe.exe'
$buildScript = Join-Path $projectRoot 'installer\Build-Installer.ps1'

try {
$requiredReleasePaths = @(
    'HuahaiClipboard.App.exe'
    'HuahaiClipboard.App.dll'
    'HuahaiClipboard.App.deps.json'
    'HuahaiClipboard.App.runtimeconfig.json'
    'HuahaiClipboard.App.pri'
    'HuahaiClipboard.Core.dll'
    'Microsoft.WinUI.dll'
    'Microsoft.Web.WebView2.Core.dll'
    'Microsoft.WindowsAppRuntime.Bootstrap.Net.dll'
    'Microsoft.Windows.SDK.NET.dll'
    'WinRT.Runtime.dll'
    'App.xbf'
    'Presentation\Windows\CursorPanelWindow.xbf'
    'Assets\Web\product-shell.html'
    'WebView2Loader.dll'
    'Microsoft.WindowsAppRuntime.Bootstrap.dll'
)

# 构造完整应用目录和被替换的前置包，验证构建器检查内容而不是文件名。
foreach ($releaseRoot in @($dotNetReleaseRoot, $windowsReleaseRoot, $webViewReleaseRoot)) {
    foreach ($relativePath in $requiredReleasePaths) {
        $target = Join-Path $releaseRoot $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Set-Content -LiteralPath $target -Value 'fixture' -Encoding ASCII
    }
}

$dotNetName = 'windowsdesktop-runtime-8.0.29-win-x64.exe'
$windowsName = 'WindowsAppRuntimeInstall-x64.exe'
$webViewName = 'MicrosoftEdgeWebView2RuntimeInstallerX64.exe'

function New-PrerequisiteFixture([string]$root, [ValidateSet('dotnet', 'windows', 'webview')][string]$tamperedComponent) {
    New-Item -ItemType Directory -Path $root -Force | Out-Null
    $dotNetPath = Join-Path $root $dotNetName
    $windowsPath = Join-Path $root $windowsName
    $webViewPath = Join-Path $root $webViewName
    Set-Content -LiteralPath $dotNetPath -Value 'dotnet-runtime-fixture' -Encoding ASCII
    Set-Content -LiteralPath $windowsPath -Value 'windows-runtime-fixture' -Encoding ASCII
    Set-Content -LiteralPath $webViewPath -Value 'webview-runtime-fixture' -Encoding ASCII
    $dotNetHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $dotNetPath).Hash.ToLowerInvariant()
    $windowsHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $windowsPath).Hash.ToLowerInvariant()
    $webViewHash = (Get-FileHash -Algorithm SHA512 -LiteralPath $webViewPath).Hash.ToLowerInvariant()
    @{
        DotNet = @{ FileName = $dotNetName; Version = '8.0.29'; Sha512 = $(if ($tamperedComponent -eq 'dotnet') { '0' * 128 } else { $dotNetHash }) }
        WindowsAppRuntime = @{ FileName = $windowsName; Version = '1.7'; Sha512 = $(if ($tamperedComponent -eq 'windows') { '0' * 128 } else { $windowsHash }) }
        WebView2Runtime = @{ FileName = $webViewName; Version = 'Evergreen'; Sha512 = $(if ($tamperedComponent -eq 'webview') { '0' * 128 } else { $webViewHash }) }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $root 'prerequisites.json') -Encoding UTF8
}

New-PrerequisiteFixture $dotNetPrerequisiteRoot 'dotnet'
New-PrerequisiteFixture $windowsPrerequisiteRoot 'windows'
New-PrerequisiteFixture $webViewPrerequisiteRoot 'webview'

$dotNetFailure = $null
try {
    & $buildScript -PublishRoot $dotNetReleaseRoot -OutputPath $probeOutput -PrerequisiteRoot $dotNetPrerequisiteRoot
} catch {
    $dotNetFailure = $_.Exception.Message
}
if ($dotNetFailure -notmatch '\.NET prerequisite SHA-512 does not match') {
    throw "Unexpected .NET prerequisite integrity result: $dotNetFailure"
}

$windowsFailure = $null
try {
    & $buildScript -PublishRoot $windowsReleaseRoot -OutputPath $probeOutput -PrerequisiteRoot $windowsPrerequisiteRoot
} catch {
    $windowsFailure = $_.Exception.Message
}
if ($windowsFailure -notmatch 'Windows App Runtime prerequisite SHA-512 does not match') {
    throw "Unexpected Windows App Runtime prerequisite integrity result: $windowsFailure"
}

$webViewFailure = $null
try {
    & $buildScript -PublishRoot $webViewReleaseRoot -OutputPath $probeOutput -PrerequisiteRoot $webViewPrerequisiteRoot
} catch {
    $webViewFailure = $_.Exception.Message
}
if ($webViewFailure -notmatch 'Evergreen WebView2 Runtime prerequisite SHA-512 does not match') {
    throw "Unexpected WebView2 Runtime prerequisite integrity result: $webViewFailure"
}

[pscustomobject]@{
    Status = 'passed'
    DotNetHash = $dotNetFailure
    WindowsAppRuntimeHash = $windowsFailure
    WebView2RuntimeHash = $webViewFailure
} | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixtureRoot = [System.IO.Path]::GetFullPath($fixtureRoot)
        $resolvedParent = [System.IO.Path]::GetDirectoryName($resolvedFixtureRoot)
        $leaf = Split-Path -Leaf $resolvedFixtureRoot
        $isOwnedParent = $resolvedParent.Equals(
            $fixtureParent,
            [System.StringComparison]::OrdinalIgnoreCase
        )

        if (-not $isOwnedParent -or $leaf -notmatch '^installer-integrity-fixture-[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected installer integrity fixture path: $resolvedFixtureRoot"
        }

        Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
    }
}
