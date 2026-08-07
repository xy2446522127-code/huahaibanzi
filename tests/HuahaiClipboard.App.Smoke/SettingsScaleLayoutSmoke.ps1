param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9321
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$webProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$oldArgs = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
$initialRatio = $null

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            $ready = $true
            break
        }
        catch { }
    }
    if (-not $ready) { throw 'Installed WebView did not expose the bounded smoke endpoint.' }

    $open = node $webProbe $DebugPort open-settings | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $open.opened) { throw 'Settings surface did not open.' }
    $initialScale = node $webProbe $DebugPort current-scale | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $null -eq $initialScale.ratio) { throw 'Initial panel scale could not be read.' }
    $initialRatio = [double]$initialScale.ratio

    $signatures = @{}
    foreach ($sample in @(
        @{ Name = '80'; Ratio = 0.8 },
        @{ Name = '100'; Ratio = 1.0 },
        @{ Name = '160'; Ratio = 1.6 }
    )) {
        $scaled = node $webProbe $DebugPort set-scale $sample.Ratio | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $scaled.scaled) { throw "$($sample.Name) percent scale did not apply." }
        $signatures[$sample.Name] = node $webProbe $DebugPort scale-layout-signature | ConvertFrom-Json
    }

    $referenceLines = ($signatures['100'].lines -join '|')
    foreach ($sampleName in @('80', '160')) {
        $sampleLines = ($signatures[$sampleName].lines -join '|')
        if ($referenceLines -ne $sampleLines) {
            throw "Uniform scaling reflowed settings helper text. 100=$referenceLines $sampleName=$sampleLines Metrics100=$($signatures['100'].layout | ConvertTo-Json -Compress) Metrics$sampleName=$($signatures[$sampleName].layout | ConvertTo-Json -Compress)"
        }
    }

    [pscustomobject]@{
        Status = 'passed'
        Lines = $referenceLines
        Panel100 = "$($signatures['100'].panelRect.width)x$($signatures['100'].panelRect.height)"
        Panel80 = "$($signatures['80'].panelRect.width)x$($signatures['80'].panelRect.height)"
        Panel160 = "$($signatures['160'].panelRect.width)x$($signatures['160'].panelRect.height)"
    } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $initialRatio -and -not $process.HasExited) {
        $null = node $webProbe $DebugPort set-scale $initialRatio
        Start-Sleep -Milliseconds 250
    }
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    if ($null -eq $oldArgs) {
        Remove-Item Env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS -ErrorAction SilentlyContinue
    }
    else {
        $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $oldArgs
    }
}
