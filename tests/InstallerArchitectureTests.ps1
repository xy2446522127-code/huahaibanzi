$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$setupPath = Join-Path $projectRoot 'dist\HuahaiClipboard-Setup.exe'
if (-not (Test-Path -LiteralPath $setupPath)) { throw "Missing setup artifact: $setupPath" }

# 读取 PE COFF Machine 字段，Windows x64 必须是 AMD64 0x8664。
$bytes = [System.IO.File]::ReadAllBytes($setupPath)
if ($bytes.Length -lt 0x40 -or $bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { throw 'Setup is not a PE executable.' }
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
if ($peOffset -lt 0 -or $peOffset + 6 -gt $bytes.Length) { throw 'Setup PE header is invalid.' }
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
if ($machine -ne 0x8664) { throw ('Setup must be AMD64 (0x8664), actual machine is 0x{0:X4}.' -f $machine) }

[pscustomobject]@{ Status = 'passed'; Machine = ('0x{0:X4}' -f $machine); Setup = $setupPath } | ConvertTo-Json -Compress
