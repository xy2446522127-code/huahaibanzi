$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fixtureRoot = Join-Path $projectRoot ('dist\installer-integrity-fixture-' + [guid]::NewGuid().ToString('N'))
$releaseRoot = Join-Path $fixtureRoot 'release'
$prerequisiteRoot = Join-Path $fixtureRoot 'prerequisites'
$probeOutput = Join-Path $fixtureRoot 'probe.exe'
$buildScript = Join-Path $projectRoot 'installer\Build-Installer.ps1'

$requiredReleasePaths = @(
    'HuahaiClipboard.NativeUiSpike.exe'
    'HuahaiClipboard.NativeUiSpike.dll'
    'HuahaiClipboard.NativeUiSpike.deps.json'
    'HuahaiClipboard.NativeUiSpike.runtimeconfig.json'
    'HuahaiClipboard.Core.dll'
    'Microsoft.Windows.SDK.NET.dll'
    'WinRT.Runtime.dll'
)

# 构造完整应用目录和被替换的前置包，验证构建器检查内容而不是文件名。
foreach ($relativePath in $requiredReleasePaths) {
    $target = Join-Path $releaseRoot $relativePath
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Set-Content -LiteralPath $target -Value 'fixture' -Encoding ASCII
}

New-Item -ItemType Directory -Path $prerequisiteRoot -Force | Out-Null
$dotNetName = 'windowsdesktop-runtime-8.0.29-win-x64.exe'
Set-Content -LiteralPath (Join-Path $prerequisiteRoot $dotNetName) -Value 'tampered-dotnet' -Encoding ASCII
@{
    DotNet = @{ FileName = $dotNetName; Version = '8.0.29'; Sha512 = ('0' * 128) }
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $prerequisiteRoot 'prerequisites.json') -Encoding UTF8

$manifestPath = Join-Path $prerequisiteRoot 'prerequisites.json'
$failure = $null
try {
    & $buildScript -PublishRoot $releaseRoot -OutputPath $probeOutput -PrerequisiteRoot $prerequisiteRoot
} catch {
    $failure = $_.Exception.Message
}

if ($failure -notmatch '\.NET prerequisite SHA-512 does not match') {
    throw "Unexpected prerequisite integrity result: $failure"
}

[pscustomobject]@{
    Status = 'passed'
    DotNetHash = $failure
} | ConvertTo-Json -Compress
