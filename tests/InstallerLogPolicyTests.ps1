$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $projectRoot 'installer\InstallerLogPolicy.cs'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing installer log policy: $source" }

Add-Type -TypeDefinition (Get-Content -LiteralPath $source -Raw -Encoding UTF8)
$path = [InstallerLogPolicy]::ResolvePath(
    'C:\Users\Test\AppData\Local\Temp',
    [datetime]::Parse('2026-08-07T22:45:03.123'))
$expected = 'C:\Users\Test\AppData\Local\Temp\HuahaiClipboard\Installer\install-20260807-224503123.log'
if ($path -ne $expected) { throw "Unexpected installer log path: $path" }

[pscustomobject]@{ Status = 'passed'; Path = $path } | ConvertTo-Json -Compress
