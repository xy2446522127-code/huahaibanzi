$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureParent = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'))
$fixtureRoot = Join-Path $fixtureParent ('installer-policy-fixture-' + [guid]::NewGuid().ToString('N'))
$missingWebViewEntryRoot = Join-Path $fixtureRoot 'missing-webview-entry'
$missingShellRoot = Join-Path $fixtureRoot 'missing-web-shell'
$missingXbfRoot = Join-Path $fixtureRoot 'missing-xbf'
$missingRuntimeBridgeRoot = Join-Path $fixtureRoot 'missing-webview-loader'
$probeOutput = Join-Path $fixtureRoot 'installer-validation-probe.exe'
$buildScript = Join-Path $projectRoot 'installer\Build-Installer.ps1'
$prerequisiteRoot = Join-Path $projectRoot 'dist\prerequisites'
$requiredPaths = @(
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
    'Assets\Web\panel-scale.js'
    'Assets\Web\virtual-record-list.js'
    'WebView2Loader.dll'
    'Microsoft.WindowsAppRuntime.Bootstrap.dll'
)

try {
# 创建最小合成 Release 目录，使测试不依赖历史 dist 产物。
foreach ($fixture in @(
    [pscustomobject]@{ Root = $missingWebViewEntryRoot; Omitted = 'HuahaiClipboard.App.exe' },
    [pscustomobject]@{ Root = $missingShellRoot; Omitted = 'Assets\Web\product-shell.html' },
    [pscustomobject]@{ Root = $missingXbfRoot; Omitted = 'Presentation\Windows\CursorPanelWindow.xbf' },
    [pscustomobject]@{ Root = $missingRuntimeBridgeRoot; Omitted = 'WebView2Loader.dll' }
)) {
    foreach ($relativePath in $requiredPaths) {
        if ($relativePath -eq $fixture.Omitted) { continue }
        $target = Join-Path $fixture.Root $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Set-Content -LiteralPath $target -Value 'fixture' -Encoding ASCII
    }
}

$missingWebViewEntryFailure = $null
try {
    & $buildScript -PublishRoot $missingWebViewEntryRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingWebViewEntryFailure = $_.Exception.Message
}
if ($missingWebViewEntryFailure -notmatch 'Release directory is incomplete: HuahaiClipboard\.App\.exe') {
    throw "Unexpected WebView entry validation failure: $missingWebViewEntryFailure"
}

$missingShellFailure = $null
try {
    & $buildScript -PublishRoot $missingShellRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingShellFailure = $_.Exception.Message
}
if ($missingShellFailure -notmatch 'Release directory is incomplete: Assets\\Web\\product-shell\.html') {
    throw "Unexpected web shell validation failure: $missingShellFailure"
}

$missingXbfFailure = $null
try {
    & $buildScript -PublishRoot $missingXbfRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingXbfFailure = $_.Exception.Message
}
if ($missingXbfFailure -notmatch 'Release directory is incomplete: Presentation\\Windows\\CursorPanelWindow\.xbf') {
    throw "Unexpected XBF validation failure: $missingXbfFailure"
}

$missingRuntimeBridgeFailure = $null
try {
    & $buildScript -PublishRoot $missingRuntimeBridgeRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingRuntimeBridgeFailure = $_.Exception.Message
}
if ($missingRuntimeBridgeFailure -notmatch 'Release directory is incomplete: WebView2Loader\.dll') {
    throw "Unexpected WebView loader validation failure: $missingRuntimeBridgeFailure"
}

[pscustomobject]@{
    Status = 'passed'
    MissingWebViewEntry = $missingWebViewEntryFailure
    MissingShell = $missingShellFailure
    MissingXbf = $missingXbfFailure
    MissingRuntimeBridge = $missingRuntimeBridgeFailure
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

        if (-not $isOwnedParent -or $leaf -notmatch '^installer-policy-fixture-[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected installer policy fixture path: $resolvedFixtureRoot"
        }

        Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
    }
}
