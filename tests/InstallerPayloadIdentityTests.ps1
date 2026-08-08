param(
    [string]$InstallerPath = (Join-Path $PSScriptRoot '..\dist\HuahaiClipboard-Setup.exe')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$resolvedInstaller = (Resolve-Path -LiteralPath $InstallerPath).Path
$assembly = [System.Reflection.Assembly]::LoadFile($resolvedInstaller)
$payload = $assembly.GetManifestResourceStream('HuahaiClipboard.Payload')
if ($null -eq $payload) {
    throw 'Installer payload resource is missing.'
}
$validationRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboard.PayloadValidation.' + [guid]::NewGuid().ToString('N'))

try {
    $archive = [System.IO.Compression.ZipArchive]::new(
        $payload,
        [System.IO.Compression.ZipArchiveMode]::Read,
        $false)
    try {
        $entryNames = @($archive.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
        if ($entryNames -notcontains 'HuahaiClipboard.App.exe') {
            throw 'Installer renamed the WinUI executable and broke its PRI/XBF resource identity.'
        }
        if ($entryNames -contains 'HuahaiClipboard.exe') {
            throw 'Installer must not ship a renamed copy of the WinUI executable.'
        }
        if (@($entryNames | Where-Object { $_ -match '\.WebView2/' }).Count -gt 0) {
            throw 'Installer must not ship a generated WebView2 user-data profile.'
        }
        foreach ($requiredResource in @('HuahaiClipboard.App.pri', 'App.xbf', 'Presentation/Windows/CursorPanelWindow.xbf')) {
            if ($entryNames -notcontains $requiredResource) {
                throw "Installer payload is missing WinUI resource: $requiredResource"
            }
        }
        if ($entryNames -notcontains 'Assets/Web/panel-scale.js') {
            throw 'Installer payload is missing the panel scale runtime module.'
        }

        [System.IO.Directory]::CreateDirectory($validationRoot) | Out-Null
        foreach ($entry in $archive.Entries) {
            if ([string]::IsNullOrEmpty($entry.Name)) { continue }
            $target = Join-Path $validationRoot ($entry.FullName -replace '/', '\')
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($target)) | Out-Null
            [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
        }
        Remove-Item -LiteralPath (Join-Path $validationRoot 'Assets\Web\panel-scale.js') -Force

        $bootstrapperType = $assembly.GetType('Bootstrapper', $true)
        $validatePayload = $bootstrapperType.GetMethod(
            'ValidatePayload',
            [System.Reflection.BindingFlags]'NonPublic,Static')
        $validationFailure = $null
        try {
            $validationArguments = [object[]]@([string]$validationRoot)
            $validatePayload.Invoke($null, $validationArguments) | Out-Null
        }
        catch {
            $validationFailure = if ($null -ne $_.Exception.InnerException) {
                $_.Exception.InnerException.Message
            }
            else {
                $_.Exception.Message
            }
        }
        if ($validationFailure -notmatch 'Assets\\Web\\panel-scale\.js') {
            throw "Bootstrapper did not reject a missing panel-scale.js payload: $validationFailure"
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $payload.Dispose()
    if (Test-Path -LiteralPath $validationRoot) {
        Remove-Item -LiteralPath $validationRoot -Recurse -Force
    }
}

[pscustomobject]@{
    Status = 'passed'
    Installer = $resolvedInstaller
    EntryPoint = 'HuahaiClipboard.App.exe'
} | ConvertTo-Json -Compress
