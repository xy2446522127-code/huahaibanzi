param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9329
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$webProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$oldArgs = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        try { $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1; $ready = $true; break } catch { }
    }
    if (-not $ready) { throw 'Installed WebView did not expose the bounded smoke endpoint.' }

    $open = node $webProbe $DebugPort open-settings | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $open.opened) { throw 'Settings surface did not open.' }
    $probe = node $webProbe $DebugPort drag-scale-pointer | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Pointer drag probe failed.' }
    $values = @($probe.samples | ForEach-Object { [int]$_.value })
    $regressions = @()
    for ($index = 1; $index -lt $values.Count; $index++) {
        if ($values[$index] -lt $values[$index - 1]) { $regressions += "$($values[$index - 1])->$($values[$index])" }
    }
    if ($regressions.Count -gt 0) { throw "Scale pointer drag regressed: $($regressions -join ', ')" }
    if ($probe.final.value -ne 160) { throw "Scale pointer drag did not reach 160%; final=$($probe.final.value)" }
    [pscustomobject]@{
        Status = 'passed'
        Samples = $values.Count
        First = $values[0]
        Last = $values[-1]
        Regressions = $regressions.Count
        StartTrack = "$($probe.start.left)-$($probe.start.right)"
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    if ($null -eq $oldArgs) { Remove-Item Env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS -ErrorAction SilentlyContinue }
    else { $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $oldArgs }
}
