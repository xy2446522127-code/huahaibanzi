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
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.CaptureSmoke.' + [guid]::NewGuid().ToString('N'))
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
$process = $null

try {
    $process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
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

    $cleared = node $probe $DebugPort clear-all | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $cleared.cleared) { throw 'Isolated screenshot history could not be cleared.' }
    $fixture = node $probe $DebugPort seed-visual-fixture | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $fixture.seeded -or $fixture.count -ne 6) { throw 'Deterministic screenshot fixture failed.' }

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
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot) -match '^HuahaiClipboard\.CaptureSmoke\.[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
