param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9241
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$webProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$priorDebug = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$priorDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$priorInstallRoot = $env:HUAHAI_CLIPBOARD_INSTALL_ROOT
$priorUserKey = $env:HUAHAI_CLIPBOARD_USER_KEY
$result = $null

Remove-Item Env:HUAHAI_CLIPBOARD_LOCALAPPDATA -ErrorAction SilentlyContinue
Remove-Item Env:HUAHAI_CLIPBOARD_INSTALL_ROOT -ErrorAction SilentlyContinue
Remove-Item Env:HUAHAI_CLIPBOARD_USER_KEY -ErrorAction SilentlyContinue
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

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

    $foxResult = node $webProbe $DebugPort settings-home | ConvertFrom-Json
    $interiorScale = node $webProbe $DebugPort set-scale 1.17 | ConvertFrom-Json
    $scrub = node $webProbe $DebugPort scrub-scale '117,83,149,81,159,100' | ConvertFrom-Json
    if (-not $foxResult.returned -or $foxResult.hash -ne '#panel') { throw 'Settings fox return failed.' }
    if (-not $interiorScale.scaled -or $interiorScale.label -ne '117%') { throw '117 percent scale failed.' }
    if ($scrub.blankSamples -ne 0 -or $scrub.finalLabel -ne '100%') { throw 'Scale preview continuity failed.' }

    $sid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $settingsPath = Join-Path (Split-Path $resolvedExe) "Data\$sid\settings.json"
    if (-not (Test-Path -LiteralPath $settingsPath)) {
        throw "Settings were not written beneath the installed executable: $settingsPath"
    }
    $saved = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
    if ([Math]::Abs([double]$saved.Appearance.PanelScale - 1.0) -gt 0.0001) {
        throw "Final committed scale was $($saved.Appearance.PanelScale), expected 1.0."
    }

    $result = [pscustomobject]@{
        Status = 'passed'
        FoxReturn = $true
        InteriorScale = $interiorScale.actual
        RapidSamples = $scrub.samples.Count
        BlankSamples = $scrub.blankSamples
        FinalScale = $saved.Appearance.PanelScale
        SettingsPath = $settingsPath
        OldAppDataExists = Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'HuahaiClipboard')
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    }
    if ($null -eq $priorDebug) { Remove-Item Env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS -ErrorAction SilentlyContinue }
    else { $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $priorDebug }
    if ($null -eq $priorDataRoot) { Remove-Item Env:HUAHAI_CLIPBOARD_LOCALAPPDATA -ErrorAction SilentlyContinue }
    else { $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $priorDataRoot }
    if ($null -eq $priorInstallRoot) { Remove-Item Env:HUAHAI_CLIPBOARD_INSTALL_ROOT -ErrorAction SilentlyContinue }
    else { $env:HUAHAI_CLIPBOARD_INSTALL_ROOT = $priorInstallRoot }
    if ($null -eq $priorUserKey) { Remove-Item Env:HUAHAI_CLIPBOARD_USER_KEY -ErrorAction SilentlyContinue }
    else { $env:HUAHAI_CLIPBOARD_USER_KEY = $priorUserKey }
}

$result | ConvertTo-Json -Compress
