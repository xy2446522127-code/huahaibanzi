param(
    [Parameter(Mandatory = $true)]
    [string] $ExePath,

    [int] $TimeoutSeconds = 10
)

$resolvedExe = (Resolve-Path -LiteralPath $ExePath -ErrorAction Stop).Path
$exeDirectory = Split-Path $resolvedExe
$requiredFiles = @(
    'HuahaiClipboard.App.dll',
    'HuahaiClipboard.App.runtimeconfig.json'
)
foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $exeDirectory $requiredFile))) {
        throw "Executable must stay with $requiredFile in the same folder."
    }
}

$process = Start-Process -FilePath $resolvedExe -WorkingDirectory (Split-Path $resolvedExe) -PassThru
$windowObserved = $false
$windowHandle = [IntPtr]::Zero

try {
    for ($second = 1; $second -le $TimeoutSeconds; $second++) {
        Start-Sleep -Seconds 1
        $process.Refresh()

        if ($process.HasExited) {
            if ($windowObserved) {
                throw "Application showed a window but exited during the $TimeoutSeconds-second stability check. ExitCode=$($process.ExitCode)"
            }

            throw "Application exited before showing a window. ExitCode=$($process.ExitCode)"
        }

        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            $windowObserved = $true
            $windowHandle = $process.MainWindowHandle
        }
    }

    if (-not $windowObserved) {
        throw "Application stayed alive but did not show a top-level window within $TimeoutSeconds seconds."
    }

    [pscustomobject]@{
        Status = 'passed'
        ProcessId = $process.Id
        MainWindowHandle = $windowHandle
        ExePath = $resolvedExe
    } | ConvertTo-Json -Compress
}
finally {
    if (-not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(2000)) {
            Stop-Process -Id $process.Id -Force
        }
    }
}
