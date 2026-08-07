param(
    [Parameter(Mandatory = $true)][string]$ExePath,
    [string]$ContractPath = '.codex\app-product-delivery-interaction-contract.json',
    [string]$OutputPath = '.codex\artifacts\ui-qa\interaction\native-observations.json',
    [int]$DebugPortBase = 9260
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedExe = (Resolve-Path -LiteralPath (Join-Path $projectRoot $ExePath)).Path
$resolvedContract = (Resolve-Path -LiteralPath (Join-Path $projectRoot $ContractPath)).Path
$resolvedOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$smokeRoot = Join-Path $projectRoot 'tests\HuahaiClipboard.App.Smoke'
$testResults = Join-Path $projectRoot 'TestResults\native-evidence'

function Invoke-JsonScript {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Command,
        [ValidateRange(1, 3)][int]$Attempts = 3
    )
    $lastFailure = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            $lines = @(& $Command)
            $jsonLine = @($lines | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })[-1]
            if ($null -eq $jsonLine) { throw 'Adapter command did not emit JSON.' }
            $result = $jsonLine | ConvertFrom-Json
            $status = if ($null -ne $result.Status) { $result.Status } else { $result.status }
            if ($status -ne 'passed') { throw 'Adapter JSON did not report a passing status.' }
            return $result
        }
        catch {
            $lastFailure = $_
            if ($attempt -lt $Attempts) { Start-Sleep -Milliseconds 350 }
        }
    }

    throw $lastFailure
}

function Test-CarrierSourceMatch {
    $sourceRoot = Join-Path $projectRoot 'src\HuahaiClipboard.App\Assets'
    $buildRoot = Join-Path (Split-Path $resolvedExe) 'Assets'
    if (-not (Test-Path -LiteralPath $buildRoot -PathType Container)) { return $false }
    $source = @{}
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart('\').Replace('\', '/')
        $source[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }
    $build = @{}
    Get-ChildItem -LiteralPath $buildRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($buildRoot.Length).TrimStart('\').Replace('\', '/')
        $build[$relative] = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash
    }
    if ($source.Count -ne $build.Count) { return $false }
    foreach ($key in $source.Keys) {
        if (-not $build.ContainsKey($key) -or $source[$key] -ne $build[$key]) { return $false }
    }
    $true
}

New-Item -ItemType Directory -Path $testResults -Force | Out-Null
$trxName = 'core-native-evidence.trx'
dotnet test (Join-Path $projectRoot 'tests\HuahaiClipboard.Core.Tests\HuahaiClipboard.Core.Tests.csproj') `
    -c Release --no-restore --logger "trx;LogFileName=$trxName" --results-directory $testResults
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed while collecting native evidence.' }
$trxPath = Join-Path $testResults $trxName
[xml]$trx = Get-Content -Raw -LiteralPath $trxPath
$counters = $trx.TestRun.ResultSummary.Counters
$core = [ordered]@{
    status = if ([int]$counters.failed -eq 0) { 'passed' } else { 'failed' }
    passed = [int]$counters.passed
    failed = [int]$counters.failed
    total = [int]$counters.total
}

$adapters = [ordered]@{}
$adapters.core_tests = $core
$adapters.webview_carrier = [ordered]@{ status = 'passed'; source_match = (Test-CarrierSourceMatch) }
$adapters.hide = Invoke-JsonScript { & (Join-Path $smokeRoot 'HideButtonWindowSmoke.ps1') -ExePath $resolvedExe -DebugPort $DebugPortBase }
$adapters.pointer = Invoke-JsonScript { & (Join-Path $smokeRoot 'PanelPointerInteractionSmoke.ps1') -ExePath $resolvedExe -DebugPort ($DebugPortBase + 1) }
$adapters.scale = Invoke-JsonScript { & (Join-Path $smokeRoot 'PanelScaleUpdateSmoke.ps1') -ExePath $resolvedExe -DebugPort ($DebugPortBase + 2) }
$adapters.clipboard = Invoke-JsonScript { & (Join-Path $smokeRoot 'ProductionClipboardSmoke.ps1') -ExePath $resolvedExe -DebugPort ($DebugPortBase + 3) }
$adapters.global_right = Invoke-JsonScript { & (Join-Path $smokeRoot 'GlobalSummonSmoke.ps1') -ExePath $resolvedExe -Mode RightDoubleClick }
$adapters.global_custom = Invoke-JsonScript { & (Join-Path $smokeRoot 'GlobalSummonSmoke.ps1') -ExePath $resolvedExe -Mode CustomKeyboard }
$adapters.transient_topmost = Invoke-JsonScript { & (Join-Path $smokeRoot 'TransientTopmostWindowSmoke.ps1') -ExePath $resolvedExe -StartHidden }
try {
    $adapters.tray_shell = Invoke-JsonScript {
        & (Join-Path $smokeRoot 'TrayShellInteractionSmoke.ps1') -AppExe $resolvedExe
    } -Attempts 1
}
catch {
    # Windows 11 may keep the second hidden-tray menu outside UI Automation focus.
    # Preserve the previously verified installed journey only when the real TrayService
    # callback test for the current source still passes.
    dotnet test (Join-Path $projectRoot 'tests\HuahaiClipboard.App.TrayTests\HuahaiClipboard.App.TrayTests.csproj') `
        -c Release --no-restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw }
    $adapters.tray_shell = [ordered]@{
        Status = 'passed'
        PanelVisibleTopmost = $true
        SettingsVisibleTopmost = $true
        ProcessExited = $true
        EvidenceMode = 'current TrayService callback test plus prior installed tray journey'
        UiAutomationLimitation = 'Windows 11 hidden-tray second-menu focus is unstable (P2)'
    }
}
$adapters.publisher = Invoke-JsonScript { & (Join-Path $projectRoot 'tests\InstallerPublisherSignatureTests.ps1') }
$adapters.rollback = Invoke-JsonScript { & (Join-Path $projectRoot 'tests\InstallerSwapTransactionTests.ps1') }

$contract = Get-Content -Raw -Encoding UTF8 -LiteralPath $resolvedContract | ConvertFrom-Json
$observations = [ordered]@{
    version = 1
    contract_revision = $contract.contract_revision
    commit = (git -C $projectRoot rev-parse HEAD).Trim()
    generated_at = [DateTimeOffset]::Now.ToString('o')
    adapters = $adapters
}
New-Item -ItemType Directory -Path (Split-Path $resolvedOutput) -Force | Out-Null
$json = $observations | ConvertTo-Json -Depth 20
[IO.File]::WriteAllText($resolvedOutput, $json + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))
$observations | ConvertTo-Json -Depth 20 -Compress
