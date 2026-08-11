param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9264,
    [string]$OutputPath = '.codex\artifacts\ui-qa\history-scale-performance.json'
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$probe = Join-Path $PSScriptRoot 'HistoryScalePerformanceProbe.cjs'
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.HistoryScale.' + [guid]::NewGuid().ToString('N'))
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$previousWebViewData = $env:WEBVIEW2_USER_DATA_FOLDER
$previousUserKey = $env:HUAHAI_CLIPBOARD_USER_KEY
$previousExePath = $env:HUAHAI_HISTORY_EXE_PATH
$previousExeHash = $env:HUAHAI_HISTORY_EXE_SHA256
$previousExeVersion = $env:HUAHAI_HISTORY_EXE_VERSION
$previousSourceRevision = $env:HUAHAI_HISTORY_SOURCE_REVISION
$previousSourceDirty = $env:HUAHAI_HISTORY_SOURCE_DIRTY
$previousSourceShellHash = $env:HUAHAI_HISTORY_SOURCE_SHELL_SHA256
$previousSourceVirtualHash = $env:HUAHAI_HISTORY_SOURCE_VIRTUAL_SHA256
$previousPackagedShellHash = $env:HUAHAI_HISTORY_PACKAGED_SHELL_SHA256
$previousPackagedVirtualHash = $env:HUAHAI_HISTORY_PACKAGED_VIRTUAL_SHA256
$process = $null

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $testUserKey = 'history-scale-performance-user'
    $testDataRoot = Join-Path $testRoot "Data\$testUserKey"
    New-Item -ItemType Directory -Path $testDataRoot -Force | Out-Null
    @{
        Appearance = @{ ThemeId = 'rose-purple'; Opacity = 0.88; BlurAmount = 32; ReflectionStrength = 0.72; CompactMode = $false; PanelScale = 1 }
        Motion = @{ PetalLevel = 0; ReduceMotion = $true; ClickDurationMs = 620; ReducedClickDurationMs = 120 }
        Input = @{ RightDoubleClickEnabled = $true; HotkeyEnabled = $false; ExcludedApplications = @(); CustomShortcut = '' }
        Behavior = @{ BackgroundEnabled = $true; HideOnOutsideClick = $false; AutoCleanupDays = 7; CheckUpdatesOnStartup = $false }
    } | ConvertTo-Json -Depth 5 -Compress | Set-Content -LiteralPath (Join-Path $testDataRoot 'settings.json') -Encoding utf8
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort --disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows"
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
    $env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $testRoot 'webview2'
    $env:HUAHAI_CLIPBOARD_USER_KEY = $testUserKey
    $sourceShell = Join-Path $projectRoot 'src\HuahaiClipboard.App\Assets\Web\product-shell.html'
    $sourceVirtual = Join-Path $projectRoot 'src\HuahaiClipboard.App\Assets\Web\virtual-record-list.js'
    $packagedShell = Join-Path (Split-Path $resolvedExe) 'Assets\Web\product-shell.html'
    $packagedVirtual = Join-Path (Split-Path $resolvedExe) 'Assets\Web\virtual-record-list.js'
    $env:HUAHAI_HISTORY_EXE_PATH = $resolvedExe
    $env:HUAHAI_HISTORY_EXE_SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $resolvedExe).Hash.ToLowerInvariant()
    $env:HUAHAI_HISTORY_EXE_VERSION = [Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedExe).FileVersion
    $env:HUAHAI_HISTORY_SOURCE_REVISION = (& git -C $projectRoot rev-parse HEAD).Trim()
    $sourceStatus = @(& git -C $projectRoot status --porcelain -- 'src/HuahaiClipboard.App/Assets/Web/product-shell.html' 'src/HuahaiClipboard.App/Assets/Web/virtual-record-list.js')
    $env:HUAHAI_HISTORY_SOURCE_DIRTY = ([string][bool]($sourceStatus.Count -gt 0)).ToLowerInvariant()
    $env:HUAHAI_HISTORY_SOURCE_SHELL_SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceShell).Hash.ToLowerInvariant()
    $env:HUAHAI_HISTORY_SOURCE_VIRTUAL_SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $sourceVirtual).Hash.ToLowerInvariant()
    $env:HUAHAI_HISTORY_PACKAGED_SHELL_SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagedShell).Hash.ToLowerInvariant()
    $env:HUAHAI_HISTORY_PACKAGED_VIRTUAL_SHA256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagedVirtual).Hash.ToLowerInvariant()
    $process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

    $ready = $false
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        Start-Sleep -Milliseconds 100
        $process.Refresh()
        if ($process.HasExited) { throw "Candidate app exited with code $($process.ExitCode)." }
        try {
            $targets = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            if (@($targets | Where-Object { $_.type -eq 'page' -and $_.url -like 'https://app.huahai.local/Web/product-shell.html*' }).Count -gt 0) {
                $ready = $true
                break
            }
        }
        catch { }
    }
    if (-not $ready) { throw 'Candidate WebView2 debugging endpoint did not become ready.' }

    node $probe $DebugPort $resolvedOutput
    if ($LASTEXITCODE -ne 0) { throw "History scale performance probe failed with exit code $LASTEXITCODE." }
}
finally {
    if ($null -ne $process) {
        $snapshot = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)
        $ownedIds = @($process.Id)
        $expanded = $true
        while ($expanded) {
            $expanded = $false
            $children = @($snapshot | Where-Object { $ownedIds -contains $_.ParentProcessId } | Select-Object -ExpandProperty ProcessId)
            foreach ($childId in $children) {
                if ($ownedIds -notcontains $childId) {
                    $ownedIds += [int]$childId
                    $expanded = $true
                }
            }
        }
        foreach ($ownedId in ($ownedIds | Sort-Object -Descending -Unique)) {
            Stop-Process -Id $ownedId -Force -ErrorAction SilentlyContinue
        }
        $process.Refresh()
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    if ($null -eq $previousArguments) { Remove-Item Env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS -ErrorAction SilentlyContinue } else { $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments }
    if ($null -eq $previousDataRoot) { Remove-Item Env:HUAHAI_CLIPBOARD_LOCALAPPDATA -ErrorAction SilentlyContinue } else { $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot }
    if ($null -eq $previousWebViewData) { Remove-Item Env:WEBVIEW2_USER_DATA_FOLDER -ErrorAction SilentlyContinue } else { $env:WEBVIEW2_USER_DATA_FOLDER = $previousWebViewData }
    if ($null -eq $previousUserKey) { Remove-Item Env:HUAHAI_CLIPBOARD_USER_KEY -ErrorAction SilentlyContinue } else { $env:HUAHAI_CLIPBOARD_USER_KEY = $previousUserKey }
    if ($null -eq $previousExePath) { Remove-Item Env:HUAHAI_HISTORY_EXE_PATH -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_EXE_PATH = $previousExePath }
    if ($null -eq $previousExeHash) { Remove-Item Env:HUAHAI_HISTORY_EXE_SHA256 -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_EXE_SHA256 = $previousExeHash }
    if ($null -eq $previousExeVersion) { Remove-Item Env:HUAHAI_HISTORY_EXE_VERSION -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_EXE_VERSION = $previousExeVersion }
    if ($null -eq $previousSourceRevision) { Remove-Item Env:HUAHAI_HISTORY_SOURCE_REVISION -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_SOURCE_REVISION = $previousSourceRevision }
    if ($null -eq $previousSourceDirty) { Remove-Item Env:HUAHAI_HISTORY_SOURCE_DIRTY -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_SOURCE_DIRTY = $previousSourceDirty }
    if ($null -eq $previousSourceShellHash) { Remove-Item Env:HUAHAI_HISTORY_SOURCE_SHELL_SHA256 -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_SOURCE_SHELL_SHA256 = $previousSourceShellHash }
    if ($null -eq $previousSourceVirtualHash) { Remove-Item Env:HUAHAI_HISTORY_SOURCE_VIRTUAL_SHA256 -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_SOURCE_VIRTUAL_SHA256 = $previousSourceVirtualHash }
    if ($null -eq $previousPackagedShellHash) { Remove-Item Env:HUAHAI_HISTORY_PACKAGED_SHELL_SHA256 -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_PACKAGED_SHELL_SHA256 = $previousPackagedShellHash }
    if ($null -eq $previousPackagedVirtualHash) { Remove-Item Env:HUAHAI_HISTORY_PACKAGED_VIRTUAL_SHA256 -ErrorAction SilentlyContinue } else { $env:HUAHAI_HISTORY_PACKAGED_VIRTUAL_SHA256 = $previousPackagedVirtualHash }
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot) -match '^HuahaiClipboard\.HistoryScale\.[0-9a-f]{32}$' -and
        (Test-Path -LiteralPath $resolvedTestRoot)) {
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            $ownedChildren = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
                $_.Name -eq 'msedge.exe' -and $_.CommandLine -like "*$resolvedTestRoot*"
            })
            if ($ownedChildren.Count -eq 0) { break }
            foreach ($child in $ownedChildren) { Stop-Process -Id $child.ProcessId -Force -ErrorAction SilentlyContinue }
            Start-Sleep -Milliseconds 100
        }
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            try { [IO.Directory]::Delete($resolvedTestRoot, $true); break }
            catch {
                if ($attempt -eq 49) { throw }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
