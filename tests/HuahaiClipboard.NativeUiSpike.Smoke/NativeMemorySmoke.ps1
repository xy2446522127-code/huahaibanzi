param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath,

    [ValidateRange(10, 180)]
    [int]$DurationSeconds = 60,

    [ValidateRange(1, 15)]
    [int]$SampleIntervalSeconds = 5
)

$ErrorActionPreference = 'Stop'
$resolvedExe = (Resolve-Path -LiteralPath $ExePath).Path
$temporaryBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$dataRoot = Join-Path $temporaryBase ("HuahaiClipboard-Memory-{0}" -f [Guid]::NewGuid().ToString('N'))
$previousOverride = $env:HUAHAI_CLIPBOARD_LOCALAPPDATA
$process = $null

function Get-Median([double[]]$Values) {
    $ordered = @($Values | Sort-Object)
    if ($ordered.Count -eq 0) { return 0 }
    $middle = [Math]::Floor($ordered.Count / 2)
    if ($ordered.Count % 2 -eq 1) { return $ordered[$middle] }
    return ($ordered[$middle - 1] + $ordered[$middle]) / 2
}

try {
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $dataRoot
    $process = Start-Process -FilePath $resolvedExe -ArgumentList '--background' -PassThru
    if (-not $process.WaitForInputIdle(5000)) { throw 'Background process did not become input-idle.' }

    $privateSamples = [Collections.Generic.List[double]]::new()
    $workingSetSamples = [Collections.Generic.List[double]]::new()
    $childProcessMaximum = 0
    $deadline = [DateTime]::UtcNow.AddSeconds($DurationSeconds)
    do {
        Start-Sleep -Seconds $SampleIntervalSeconds
        $process.Refresh()
        if ($process.HasExited) { throw 'Background process exited during memory measurement.' }
        $privateSamples.Add($process.PrivateMemorySize64 / 1MB)
        $workingSetSamples.Add($process.WorkingSet64 / 1MB)
        $children = @(Get-CimInstance Win32_Process -Filter ("ParentProcessId={0}" -f $process.Id) -ErrorAction SilentlyContinue)
        $childProcessMaximum = [Math]::Max($childProcessMaximum, $children.Count)
    } while ([DateTime]::UtcNow -lt $deadline)

    [pscustomobject]@{
        DurationSeconds = $DurationSeconds
        Samples = $privateSamples.Count
        PrivateMemoryMedianMB = [Math]::Round((Get-Median $privateSamples.ToArray()), 2)
        PrivateMemoryMaximumMB = [Math]::Round(($privateSamples | Measure-Object -Maximum).Maximum, 2)
        WorkingSetMedianMB = [Math]::Round((Get-Median $workingSetSamples.ToArray()), 2)
        WorkingSetMaximumMB = [Math]::Round(($workingSetSamples | Measure-Object -Maximum).Maximum, 2)
        ChildProcessMaximum = $childProcessMaximum
    } | ConvertTo-Json -Compress
}
finally {
    $env:HUAHAI_CLIPBOARD_LOCALAPPDATA = $previousOverride
    if ($process) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
        }
        catch {
        }
    }

    $resolvedDataRoot = [IO.Path]::GetFullPath($dataRoot)
    $safeLeaf = (Split-Path $resolvedDataRoot -Leaf).StartsWith('HuahaiClipboard-Memory-')
    if ($safeLeaf -and $resolvedDataRoot.StartsWith($temporaryBase, [StringComparison]::OrdinalIgnoreCase)) {
        Remove-Item -LiteralPath $resolvedDataRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}
