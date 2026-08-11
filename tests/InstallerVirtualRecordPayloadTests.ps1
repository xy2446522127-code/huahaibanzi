$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureParent = Join-Path ([System.IO.Path]::GetTempPath()) 'HuahaiClipboard.Tests'
$fixtureRoot = Join-Path $fixtureParent ('installer-virtual-record-payload-' + [guid]::NewGuid().ToString('N'))
$publishRoot = Join-Path $fixtureRoot 'publish'
$prerequisiteRoot = Join-Path $fixtureRoot 'prerequisites'
$outputPath = Join-Path $fixtureRoot 'probe.exe'
$buildScript = Join-Path $projectRoot 'installer\Build-Installer.ps1'

try {
    $requiredWithoutVirtualRecordList = @(
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
        'WebView2Loader.dll'
        'Microsoft.WindowsAppRuntime.Bootstrap.dll'
    )
    foreach ($relativePath in $requiredWithoutVirtualRecordList) {
        $target = Join-Path $publishRoot $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Set-Content -LiteralPath $target -Value 'fixture' -Encoding ASCII
    }
    New-Item -ItemType Directory -Path $prerequisiteRoot -Force | Out-Null

    $failure = $null
    try {
        & $buildScript -PublishRoot $publishRoot -OutputPath $outputPath -PrerequisiteRoot $prerequisiteRoot
    }
    catch {
        $failure = $_.Exception.Message
    }

    if ($failure -notmatch 'Assets\\Web\\virtual-record-list\.js') {
        throw "Installer builder did not reject the missing virtual-record-list.js payload: $failure"
    }

    [pscustomobject]@{
        Status = 'passed'
        MissingResource = 'Assets\Web\virtual-record-list.js'
    } | ConvertTo-Json -Compress
}
finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = [System.IO.Path]::GetFullPath($fixtureRoot)
        $resolvedParent = [System.IO.Path]::GetDirectoryName($resolvedFixture)
        if (-not $resolvedParent.Equals(
            [System.IO.Path]::GetFullPath($fixtureParent),
            [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean unexpected fixture path: $resolvedFixture"
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
