$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$expected = '1.1.13'

$app = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'src\HuahaiClipboard.App\Presentation\Windows\CursorPanelWindow.xaml.cs')
$shell = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'src\HuahaiClipboard.App\Assets\Web\product-shell.html')
$installer = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\Bootstrapper.cs')
$readme = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'README.md')

if ($app -notmatch 'CurrentVersion = new\(1, 1, 13\)') { throw 'Application update version is not 1.1.13.' }
if (([regex]::Matches($shell, '1\.1\.13')).Count -lt 2 -or $shell -match '1\.1\.10') { throw 'About page version is not 1.1.13.' }
if ($installer -notmatch 'AssemblyVersion\("1\.1\.13\.0"\)' -or
    $installer -notmatch 'AssemblyFileVersion\("1\.1\.13\.0"\)' -or
    $installer -notmatch 'SetValue\("DisplayVersion", "1\.1\.13"') {
    throw 'Installer metadata version is not 1.1.13.'
}
if ($readme -notmatch 'Version=1\.1\.13' -or $readme -notmatch 'webview-build-1\.1\.13') {
    throw 'README release commands are not pinned to 1.1.13.'
}

[pscustomobject]@{ Status = 'passed'; Version = $expected } | ConvertTo-Json -Compress
