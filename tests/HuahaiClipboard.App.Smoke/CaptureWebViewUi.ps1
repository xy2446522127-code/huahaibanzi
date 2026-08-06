param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [Parameter(Mandatory = $true)][string]$PanelOutputPath,
    [Parameter(Mandatory = $true)][string]$SettingsOutputPath,
    [int]$DebugPort = 9237,
    [int]$StabilizationMilliseconds = 5000
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$panelOutput = [IO.Path]::GetFullPath($PanelOutputPath)
$settingsOutput = [IO.Path]::GetFullPath($SettingsOutputPath)
New-Item -ItemType Directory -Path (Split-Path -Parent $panelOutput) -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $settingsOutput) -Force | Out-Null
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$probe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            break
        } catch {
            if ($attempt -eq 59) { throw 'WebView2 debugging endpoint did not become ready.' }
        }
    }

    Start-Sleep -Milliseconds $StabilizationMilliseconds
    $panel = node $probe $DebugPort capture $panelOutput | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $panel.captured) { throw 'Panel WebView screenshot failed.' }
    $settings = node $probe $DebugPort open-settings | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $settings.opened) { throw 'Settings surface did not open.' }
    Start-Sleep -Milliseconds $StabilizationMilliseconds
    $settingsCapture = node $probe $DebugPort capture $settingsOutput | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $settingsCapture.captured) { throw 'Settings WebView screenshot failed.' }

    [pscustomobject]@{
        Status = 'passed'
        Panel = $panel.output
        Settings = $settingsCapture.output
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
}
