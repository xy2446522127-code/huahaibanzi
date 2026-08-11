$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workflowPath = Join-Path $projectRoot 'docs\product\desktop-release-evidence-workflow.yml'
$adapterPath = Join-Path $projectRoot 'tests\update-evidence\HostedWindowsInstalledUpgrade.ps1'

if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) { throw 'Hosted Windows evidence workflow is missing.' }
if (-not (Test-Path -LiteralPath $adapterPath -PathType Leaf)) { throw 'Hosted Windows installed-upgrade adapter is missing.' }

$workflow = Get-Content -Raw -LiteralPath $workflowPath
$adapter = Get-Content -Raw -LiteralPath $adapterPath
$tokens = $null
$parseErrors = $null
[System.Management.Automation.Language.Parser]::ParseFile(
    $adapterPath,
    [ref]$tokens,
    [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -gt 0) {
    throw "Installed-upgrade adapter has PowerShell syntax errors: $($parseErrors[0].Message)"
}
foreach ($required in @('workflow_dispatch:', 'runs-on: windows-latest', 'v1.1.10', 'v1.1.11',
    'HostedWindowsInstalledUpgrade.ps1', 'actions/upload-artifact@v4')) {
    if (-not $workflow.Contains($required)) { throw "Workflow contract is missing: $required" }
}
foreach ($required in @('RUNNER_ENVIRONMENT', 'github-hosted', '--silent', '--no-launch', '--install-dir',
    'representative-data.json', 'Get-AuthenticodeSignature', 'PinnedPublisherThumbprint',
    'UIAutomationClient', 'MainWindowHandle', 'startup_succeeded', 'user_data_preserved')) {
    if (-not $adapter.Contains($required)) { throw "Installed-upgrade adapter contract is missing: $required" }
}

[pscustomobject]@{ Status = 'passed'; Workflow = 'desktop-release-evidence' } | ConvertTo-Json -Compress
