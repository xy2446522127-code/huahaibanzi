param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9261,
    [string]$ScreenshotPath
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runner = Join-Path $PSScriptRoot 'LongContentPreviewEndToEndSmoke.cjs'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.PreviewSmoke.' + [guid]::NewGuid().ToString('N'))
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$previousClipboard = Get-Clipboard -Raw -ErrorAction SilentlyContinue
$process = $null
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot

try {
    $process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) { throw "App exited before preview smoke. ExitCode=$($process.ExitCode)" }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            break
        } catch {
            if ($attempt -eq 79) { throw 'WebView2 debugging endpoint did not become ready.' }
        }
    }

    Start-Sleep -Milliseconds 750
    Set-Clipboard -Value ('preview-smoke-' + [guid]::NewGuid().ToString('N'))
    Start-Sleep -Milliseconds 500
    $nodeArguments = @($runner, $DebugPort)
    if (-not [string]::IsNullOrWhiteSpace($ScreenshotPath)) {
        $resolvedScreenshot = [IO.Path]::GetFullPath((Join-Path $projectRoot $ScreenshotPath))
        New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedScreenshot) -Force | Out-Null
        $nodeArguments += $resolvedScreenshot
    }
    $result = node @nodeArguments | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $result.passed -ne 8) { throw 'Long-content preview end-to-end contract failed.' }
    $result | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    if ($null -ne $previousClipboard) { Set-Clipboard -Value $previousClipboard }
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    if ((Test-Path -LiteralPath $testRoot) -and (Split-Path -Leaf $testRoot) -match '^HuahaiClipboard\.PreviewSmoke\.[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
