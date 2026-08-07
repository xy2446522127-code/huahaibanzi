$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$scriptPath = Join-Path $projectRoot 'installer\Uninstall.ps1'
$windowsPowerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$escapedPath = $scriptPath.Replace("'", "''")
$command = @"
`$tokens = `$null
`$errors = `$null
[System.Management.Automation.Language.Parser]::ParseFile('$escapedPath', [ref]`$tokens, [ref]`$errors) | Out-Null
if (`$errors.Count -gt 0) {
    `$errors | ForEach-Object { [Console]::Error.WriteLine(`$_.Message) }
    exit 1
}
exit 0
"@

$process = Start-Process -FilePath $windowsPowerShell -ArgumentList @(
    '-NoProfile',
    '-NonInteractive',
    '-Command',
    $command
) -Wait -PassThru

if ($process.ExitCode -ne 0) {
    throw 'Uninstall.ps1 is not parseable by the Windows PowerShell 5.1 host used by Add/Remove Programs.'
}

[pscustomobject]@{
    Status = 'passed'
    Host = $windowsPowerShell
    Script = $scriptPath
} | ConvertTo-Json -Compress
