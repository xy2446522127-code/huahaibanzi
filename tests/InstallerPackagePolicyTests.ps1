$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureRoot = Join-Path $projectRoot ('dist\installer-policy-fixture-' + [guid]::NewGuid().ToString('N'))
$missingNativeEntryRoot = Join-Path $fixtureRoot 'missing-native-entry'
$missingRuntimeBridgeRoot = Join-Path $fixtureRoot 'missing-runtime-bridge'
$missingManagedDependencyRoot = Join-Path $fixtureRoot 'missing-managed-dependency'
$probeOutput = Join-Path $fixtureRoot 'installer-validation-probe.exe'
$buildScript = Join-Path $projectRoot 'installer\Build-Installer.ps1'
$prerequisiteRoot = Join-Path $projectRoot 'dist\prerequisites'
$requiredPaths = @(
    'HuahaiClipboard.NativeUiSpike.exe'
    'HuahaiClipboard.NativeUiSpike.dll'
    'HuahaiClipboard.NativeUiSpike.deps.json'
    'HuahaiClipboard.NativeUiSpike.runtimeconfig.json'
    'HuahaiClipboard.Core.dll'
    'Microsoft.Windows.SDK.NET.dll'
    'WinRT.Runtime.dll'
)

# 创建两个最小合成 Release 目录，使测试不依赖历史 dist 产物。
foreach ($fixture in @(
    [pscustomobject]@{ Root = $missingNativeEntryRoot; Omitted = 'HuahaiClipboard.NativeUiSpike.exe' },
    [pscustomobject]@{ Root = $missingRuntimeBridgeRoot; Omitted = 'WinRT.Runtime.dll' },
    [pscustomobject]@{ Root = $missingManagedDependencyRoot; Omitted = 'HuahaiClipboard.Core.dll' }
)) {
    foreach ($relativePath in $requiredPaths) {
        if ($relativePath -eq $fixture.Omitted) { continue }
        $target = Join-Path $fixture.Root $relativePath
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Set-Content -LiteralPath $target -Value 'fixture' -Encoding ASCII
    }
}

$missingNativeEntryFailure = $null
try {
    & $buildScript -PublishRoot $missingNativeEntryRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingNativeEntryFailure = $_.Exception.Message
}
if ($missingNativeEntryFailure -notmatch 'Release directory is incomplete: HuahaiClipboard\.NativeUiSpike\.exe') {
    throw "Unexpected native entry validation failure: $missingNativeEntryFailure"
}

$missingRuntimeBridgeFailure = $null
try {
    & $buildScript -PublishRoot $missingRuntimeBridgeRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingRuntimeBridgeFailure = $_.Exception.Message
}
if ($missingRuntimeBridgeFailure -notmatch 'Release directory is incomplete: WinRT\.Runtime\.dll') {
    throw "Unexpected runtime bridge validation failure: $missingRuntimeBridgeFailure"
}

$missingManagedDependencyFailure = $null
try {
    & $buildScript -PublishRoot $missingManagedDependencyRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $missingManagedDependencyFailure = $_.Exception.Message
}
if ($missingManagedDependencyFailure -notmatch 'Release directory is incomplete: HuahaiClipboard\.Core\.dll') {
    throw "Unexpected managed dependency validation failure: $missingManagedDependencyFailure"
}

[pscustomobject]@{
    Status = 'passed'
    MissingNativeEntry = $missingNativeEntryFailure
    MissingRuntimeBridge = $missingRuntimeBridgeFailure
    MissingManagedDependency = $missingManagedDependencyFailure
} | ConvertTo-Json -Compress
