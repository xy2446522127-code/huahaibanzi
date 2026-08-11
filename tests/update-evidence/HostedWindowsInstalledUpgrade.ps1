param(
    [Parameter(Mandatory = $true)][string]$OldInstaller,
    [Parameter(Mandatory = $true)][string]$NewInstaller,
    [Parameter(Mandatory = $true)][string]$OldSha256,
    [Parameter(Mandatory = $true)][string]$NewSha256,
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [Parameter(Mandatory = $true)][string]$EvidencePath
)
$ErrorActionPreference = 'Stop'
$PinnedPublisherThumbprint = 'CD06B727BD8811C3B59CE0A4F9384D68EC7431C2'

if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'This destructive installed-upgrade adapter may run only on a disposable GitHub-hosted Windows runner.'
}

$OldInstaller = [IO.Path]::GetFullPath($OldInstaller)
$NewInstaller = [IO.Path]::GetFullPath($NewInstaller)
$InstallRoot = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$EvidencePath = [IO.Path]::GetFullPath($EvidencePath)
$allowedPrefix = 'D:\HuahaiClipboardEvidence\'
if (-not $InstallRoot.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    (Split-Path -Leaf $InstallRoot) -ne 'HuahaiClipboard') {
    throw "Refusing unexpected evidence install root: $InstallRoot"
}

function Assert-Package {
    param([string]$Path, [string]$ExpectedSha256)
    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedSha256.ToLowerInvariant()) { throw "Package digest mismatch: $Path" }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    $thumbprint = if ($null -eq $signature.SignerCertificate) { '' } else {
        ($signature.SignerCertificate.Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    }
    if ($signature.Status -in @(
            [System.Management.Automation.SignatureStatus]::NotSigned,
            [System.Management.Automation.SignatureStatus]::HashMismatch
        ) -or $thumbprint -ne $PinnedPublisherThumbprint) {
        throw "Package signature or pinned publisher mismatch: $Path"
    }
}

function Invoke-Installer {
    param([string]$Path)
    $process = Start-Process -FilePath $Path -ArgumentList @(
        '--silent', '--no-launch', '--install-dir', $InstallRoot
    ) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Installer failed with exit code $($process.ExitCode): $Path" }
}

function Assert-Version {
    param([string]$Expected)
    $app = Join-Path $InstallRoot 'HuahaiClipboard.App.exe'
    if (-not (Test-Path -LiteralPath $app -PathType Leaf)) { throw 'Installed application executable is missing.' }
    $actual = [Diagnostics.FileVersionInfo]::GetVersionInfo($app).FileVersion
    if (([Version]$actual).ToString(3) -ne ([Version]$Expected).ToString(3)) {
        throw "Expected installed version $Expected, found $actual."
    }
    return $app
}

function Wait-ForVisibleShell {
    param([Diagnostics.Process]$Process)
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    $expectedNames = @(
        -join @([char]0x82B1, [char]0x6D77, [char]0x526A, [char]0x8D34, [char]0x677F),
        -join @([char]0x641C, [char]0x7D22, [char]0x6587, [char]0x672C),
        (-join @([char]0x6700, [char]0x8FD1)) + ' 3 ' + [char]0x5929
    )
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        Start-Sleep -Milliseconds 500
        $Process.Refresh()
        if ($Process.HasExited) { throw "Installed application exited early with code $($Process.ExitCode)." }
        if (-not $Process.Responding -or $Process.MainWindowHandle -eq [IntPtr]::Zero) { continue }
        try {
            $root = [Windows.Automation.AutomationElement]::FromHandle($Process.MainWindowHandle)
            $elements = $root.FindAll(
                [Windows.Automation.TreeScope]::Descendants,
                [Windows.Automation.Condition]::TrueCondition)
            foreach ($element in $elements) {
                $name = $element.Current.Name
                foreach ($expectedName in $expectedNames) {
                    if ($name.Contains($expectedName)) { return $name }
                }
            }
        }
        catch [System.Windows.Automation.ElementNotAvailableException] {
            continue
        }
    }
    throw 'Installed application never exposed the expected Huahai clipboard shell through Windows UI Automation.'
}

$application = $null
$dataHash = $null
try {
    Assert-Package $OldInstaller $OldSha256
    Assert-Package $NewInstaller $NewSha256
    Invoke-Installer $OldInstaller
    $oldApp = Assert-Version '1.1.10'

    $dataFile = Join-Path $InstallRoot 'Data\evidence-user\representative-data.json'
    New-Item -ItemType Directory -Path (Split-Path -Parent $dataFile) -Force | Out-Null
    '{"favorite":true,"pinned":true,"text":"Huahai installed upgrade evidence"}' |
        Set-Content -LiteralPath $dataFile -Encoding UTF8
    $dataHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dataFile).Hash.ToLowerInvariant()

    Invoke-Installer $NewInstaller
    $newApp = Assert-Version '1.1.11'
    if (-not (Test-Path -LiteralPath $dataFile -PathType Leaf) -or
        (Get-FileHash -Algorithm SHA256 -LiteralPath $dataFile).Hash.ToLowerInvariant() -ne $dataHash) {
        throw 'Representative install-root user data was not preserved by the real installer upgrade.'
    }

    $env:HUAHAI_CLIPBOARD_USER_KEY = 'evidence-user'
    $application = Start-Process -FilePath $newApp -WorkingDirectory $InstallRoot -PassThru
    $visibleName = Wait-ForVisibleShell $application

    $evidence = [ordered]@{
        evidence_id = 'hosted_windows_installed_upgrade'
        passed = $true
        source_revision = $env:GITHUB_SHA
        workflow_run_id = $env:GITHUB_RUN_ID
        environment = 'github-hosted-windows'
        from_version = '1.1.10'
        to_version = '1.1.11'
        old_package_sha256 = $OldSha256.ToLowerInvariant()
        package_sha256 = $NewSha256.ToLowerInvariant()
        publisher_thumbprint = $PinnedPublisherThumbprint
        user_data_preserved = $true
        process_started = $true
        startup_succeeded = $true
        readiness_signal = 'windows-ui-automation-visible-shell'
        visible_shell_name = $visibleName
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $EvidencePath) -Force | Out-Null
    $evidence | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $EvidencePath -Encoding UTF8
    $evidence | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $application -and -not $application.HasExited) {
        Stop-Process -Id $application.Id -Force -ErrorAction SilentlyContinue
        $application.WaitForExit(5000) | Out-Null
    }
    Remove-Item Env:HUAHAI_CLIPBOARD_USER_KEY -ErrorAction SilentlyContinue
}
