param(
    [int]$DebugPort = 9250,
    [string]$OutputPath = '.codex\artifacts\ui-qa\interaction\web.json'
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$edgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$contractPath = Join-Path $projectRoot '.codex\app-product-delivery-interaction-contract.json'
$shellPath = Join-Path $projectRoot 'src\HuahaiClipboard.App\Assets\Web\product-shell.html'
$runnerPath = Join-Path $PSScriptRoot 'WebInteractionContractSmoke.cjs'
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = Join-Path $tempBase ('HuahaiClipboard.WebContract.' + [guid]::NewGuid().ToString('N'))
$process = $null

if (-not (Test-Path -LiteralPath $edgePath -PathType Leaf)) {
    throw "Microsoft Edge was not found: $edgePath"
}

try {
    $arguments = @(
        '--headless=new'
        "--remote-debugging-port=$DebugPort"
        "--user-data-dir=$testRoot"
        '--no-first-run'
        '--disable-extensions'
        '--disable-background-networking'
        '--allow-file-access-from-files'
        'about:blank'
    )
    $process = Start-Process -FilePath $edgePath -ArgumentList $arguments -WindowStyle Hidden -PassThru

    for ($attempt = 0; $attempt -lt 80; $attempt++) {
        try {
            $targets = Invoke-RestMethod -UseBasicParsing "http://127.0.0.1:$DebugPort/json" -TimeoutSec 1
            if (@($targets | Where-Object type -eq 'page').Count -gt 0) { break }
        }
        catch {
            if ($attempt -eq 79) { throw 'Headless Edge debugging endpoint did not become ready.' }
        }
        Start-Sleep -Milliseconds 100
    }

    node $runnerPath $DebugPort $contractPath $shellPath $resolvedOutput
    if ($LASTEXITCODE -ne 0) {
        throw "Web interaction contract failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($null -ne $process) {
        $process.Refresh()
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            Wait-Process -Id $process.Id -Timeout 10 -ErrorAction SilentlyContinue
        }
    }
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        $ownedChildren = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
            $_.Name -eq 'msedge.exe' -and $_.CommandLine -like "*$testRoot*"
        })
        if ($ownedChildren.Count -eq 0) { break }
        Start-Sleep -Milliseconds 100
    }
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempBase, [StringComparison]::OrdinalIgnoreCase) -and
        (Split-Path -Leaf $resolvedTestRoot) -match '^HuahaiClipboard\.WebContract\.[0-9a-f]{32}$' -and
        (Test-Path -LiteralPath $resolvedTestRoot)) {
        for ($attempt = 0; $attempt -lt 50; $attempt++) {
            try {
                [IO.Directory]::Delete($resolvedTestRoot, $true)
                break
            }
            catch [IO.IOException] {
                if ($attempt -eq 49) { throw }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
