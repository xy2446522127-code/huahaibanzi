param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [int]$DebugPort = 9232
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$recordProbe = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke\WebViewRecordActionsSmoke.cjs'
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.WebViewSmoke.' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$userDataRoot = Join-Path $env:LOCALAPPDATA 'HuahaiClipboard'
$userDataBefore = @{}
if (Test-Path -LiteralPath $userDataRoot) {
    Get-ChildItem -LiteralPath $userDataRoot -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($userDataRoot.Length).TrimStart('\')
        $userDataBefore[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }
}
Add-Type -AssemblyName System.Windows.Forms
$clipboardData = [System.Windows.Forms.Clipboard]::GetDataObject()
$clipboardWasEmpty = $null -eq $clipboardData
$clipboardSnapshot = [System.Windows.Forms.DataObject]::new()
foreach ($format in @($(if ($clipboardWasEmpty) { @() } else { $clipboardData.GetFormats($false) }))) {
    $value = $clipboardData.GetData($format, $false)
    if ($value -is [string]) {
        $clipboardSnapshot.SetData($format, $false, [string]$value)
        continue
    }
    if ($value -is [System.IO.MemoryStream]) {
        $clipboardSnapshot.SetData($format, $false, [System.IO.MemoryStream]::new($value.ToArray(), $false))
        continue
    }
    throw "Clipboard smoke cannot safely clone and restore format '$format' of type '$($value.GetType().FullName)'; no test changes were made."
}
function Restore-ClipboardSnapshot([System.Windows.Forms.DataObject]$snapshot, [bool]$wasEmpty) {
    for ($attempt = 0; $attempt -lt 10; $attempt++) {
        try {
            if ($wasEmpty) {
                [System.Windows.Forms.Clipboard]::Clear()
                return
            }
            [System.Windows.Forms.Clipboard]::SetDataObject($snapshot, $true)
            return
        } catch [System.Runtime.InteropServices.ExternalException] {
            if ($attempt -eq 9) { throw }
            Start-Sleep -Milliseconds 100
        }
    }
}
$deleteProbe = 'HuahaiClipboard-Smoke-' + [guid]::NewGuid().ToString('N')
$previousArguments = $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS
$previousWebViewData = $env:WEBVIEW2_USER_DATA_FOLDER
$previousDataRoot = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $testRoot
$env:WEBVIEW2_USER_DATA_FOLDER = Join-Path $testRoot 'webview2'
$env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = "--remote-debugging-port=$DebugPort"
$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru

try {
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
        if ($process.HasExited) { throw "Installed app exited with code $($process.ExitCode)." }
        try {
            $null = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            break
        } catch {
            if ($attempt -eq 39) { throw 'WebView2 debugging endpoint did not become ready.' }
        }
    }

    # The CDP endpoint can appear before the native clipboard listener is registered.
    Start-Sleep -Milliseconds 1500
    $hidden = node $recordProbe $DebugPort hide-panel | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $hidden.hidden) {
        throw 'The initialized panel could not enter background mode before clipboard capture.'
    }
    Start-Sleep -Milliseconds 350
    Set-Clipboard -Value $deleteProbe
    Start-Sleep -Milliseconds 500
    $summon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    if (-not $summon.WaitForExit(15000) -or $summon.ExitCode -ne 0) {
        throw 'The background instance could not be summoned for record interaction checks.'
    }
    Start-Sleep -Milliseconds 750
    $inspectRaw = node $recordProbe $DebugPort inspect-and-toggle $deleteProbe
    $inspectExitCode = $LASTEXITCODE
    $inspect = if ([string]::IsNullOrWhiteSpace($inspectRaw)) { $null } else { $inspectRaw | ConvertFrom-Json }
    if ($inspectExitCode -ne 0 -or $null -eq $inspect -or -not $inspect.pinRestored -or -not $inspect.favoriteRestored) {
        throw "Pin/favorite state did not toggle and restore through the production bridge. ExitCode=$inspectExitCode Raw=$inspectRaw Result=$($inspect | ConvertTo-Json -Compress)"
    }

    Restore-ClipboardSnapshot $clipboardSnapshot $clipboardWasEmpty
    $copied = node $recordProbe $DebugPort copy-id $inspect.id | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $copied.clicked) {
        throw 'A real history record could not be clicked through the production bridge.'
    }
    $clipboardMatched = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 100
        if ((Get-Clipboard -Raw -ErrorAction SilentlyContinue) -eq $inspect.text) {
            $clipboardMatched = $true
            break
        }
    }
    if (-not $clipboardMatched) { throw 'Clicking a history record did not write it back to the Windows clipboard.' }

    $deleteSummon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
    if (-not $deleteSummon.WaitForExit(15000) -or $deleteSummon.ExitCode -ne 0) {
        throw 'The background instance could not be summoned to clean the text probe.'
    }
    $deleted = node $recordProbe $DebugPort wait-and-delete-text $deleteProbe | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $deleted.deleted) {
        throw 'A real captured clipboard record could not be deleted through the production bridge.'
    }

    $imageHidden = node $recordProbe $DebugPort hide-panel | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $imageHidden.hidden) {
        throw 'The panel could not return to background mode before image capture.'
    }
    Start-Sleep -Milliseconds 350
    $bitmap = [System.Drawing.Bitmap]::new(8, 4)
    try {
        for ($x = 0; $x -lt $bitmap.Width; $x++) {
            for ($y = 0; $y -lt $bitmap.Height; $y++) {
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 214 - ($x * 8), 89 + ($y * 20), 164))
            }
        }
        [System.Windows.Forms.Clipboard]::SetImage($bitmap)
        Start-Sleep -Milliseconds 750
        $imageSummon = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru -WindowStyle Hidden
        if (-not $imageSummon.WaitForExit(15000) -or $imageSummon.ExitCode -ne 0) {
            throw 'The background instance could not be summoned for thumbnail checks.'
        }
        Start-Sleep -Milliseconds 500
        $thumbnail = node $recordProbe $DebugPort wait-thumbnail | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $thumbnail.loaded -or
            [Math]::Abs([double]$thumbnail.slotWidth - [double]$thumbnail.slotHeight) -gt 0.1 -or
            $thumbnail.objectFit -ne 'cover') {
            throw "A real image thumbnail was not square and cover-cropped. Result=$($thumbnail | ConvertTo-Json -Compress)"
        }
        $thumbnailDeleted = node $recordProbe $DebugPort wait-and-delete-text $thumbnail.title | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $thumbnailDeleted.deleted) {
            throw 'The real image thumbnail record could not be cleaned from isolated history.'
        }
    }
    finally {
        $bitmap.Dispose()
    }

    $cleanup = node $recordProbe $DebugPort delete-prefix 'HuahaiClipboard-Smoke-' | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0) { throw 'Smoke test records could not be cleaned from production history.' }

    [pscustomobject]@{
        Status = 'passed'
        LiveCount = $inspect.countText
        VisibleRows = $inspect.visibleRows
        PinRestored = $inspect.pinRestored
        FavoriteRestored = $inspect.favoriteRestored
        DeletePassed = $deleted.deleted
        StaleProbeRecordsRemoved = $cleanup.deleted
        CopyPassed = $clipboardMatched
        ThumbnailLoaded = $thumbnail.loaded
        ThumbnailSlot = "$($thumbnail.slotWidth)x$($thumbnail.slotHeight)"
        ThumbnailNaturalSize = "$($thumbnail.naturalWidth)x$($thumbnail.naturalHeight)"
        ProcessId = $process.Id
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -Force
        Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
    }
    Restore-ClipboardSnapshot $clipboardSnapshot $clipboardWasEmpty
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousDataRoot
    $env:WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS = $previousArguments
    $env:WEBVIEW2_USER_DATA_FOLDER = $previousWebViewData
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path $resolvedTestRoot -Leaf).StartsWith('HuahaiClipboard.WebViewSmoke.')) {
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            try {
                Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force -ErrorAction Stop
                break
            }
            catch [System.IO.IOException] {
                if ($attempt -eq 49) { throw }
                Start-Sleep -Milliseconds 100
            }
            catch [System.UnauthorizedAccessException] {
                if ($attempt -eq 49) { throw }
                Start-Sleep -Milliseconds 100
            }
        }
    }
    $userDataAfter = @{}
    if (Test-Path -LiteralPath $userDataRoot) {
        Get-ChildItem -LiteralPath $userDataRoot -File -Recurse | ForEach-Object {
            $relative = $_.FullName.Substring($userDataRoot.Length).TrimStart('\')
            $userDataAfter[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
        }
    }
    $userDataChanged = @($userDataBefore.Keys | Where-Object {
        -not $userDataAfter.ContainsKey($_) -or $userDataAfter[$_] -ne $userDataBefore[$_]
    })
    $userDataAdded = @($userDataAfter.Keys | Where-Object { -not $userDataBefore.ContainsKey($_) })
    if ($userDataChanged.Count -gt 0 -or $userDataAdded.Count -gt 0) {
        throw 'Production clipboard smoke modified the real HuahaiClipboard user-data directory.'
    }
    if (Test-Path -LiteralPath $resolvedTestRoot) {
        throw 'Production clipboard smoke could not remove its isolated temporary data directory.'
    }
}
