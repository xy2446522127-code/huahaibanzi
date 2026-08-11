param(
    [Parameter(Mandatory = $true)][string]$OutputPath,
    [string]$SourcePath = (Join-Path $PSScriptRoot 'HuahaiClipboard.Launcher.cpp')
)

$ErrorActionPreference = 'Stop'
$outputPath = [IO.Path]::GetFullPath($OutputPath)
$sourcePath = [IO.Path]::GetFullPath($SourcePath)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere -PathType Leaf)) { throw 'Visual Studio locator was not found.' }
$compiler = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\cl.exe' | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($compiler)) { throw 'Visual C++ x64 compiler was not found.' }

$compilerDirectory = Split-Path -Parent $compiler
$msvcRoot = [IO.Path]::GetFullPath((Join-Path $compilerDirectory '..\..\..'))
$windowsKits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10'
$sdkVersion = Get-ChildItem -LiteralPath (Join-Path $windowsKits 'Include') -Directory |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'um\Windows.h') } |
    Sort-Object { [version]$_.Name } -Descending |
    Select-Object -First 1
if ($null -eq $sdkVersion) { throw 'Windows SDK headers were not found.' }

$temporaryRoot = Join-Path ([IO.Path]::GetTempPath()) ('HuahaiClipboard.Launcher.' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $temporaryRoot -Force | Out-Null
    New-Item -ItemType Directory -Path (Split-Path -Parent $outputPath) -Force | Out-Null
    $includeRoot = $sdkVersion.FullName
    $libraryRoot = Join-Path $windowsKits ('Lib\' + $sdkVersion.Name)
    $arguments = @(
        '/nologo', '/O2', '/MT', '/EHsc', '/W4', '/WX', '/DUNICODE', '/D_UNICODE',
        ('/I' + (Join-Path $msvcRoot 'include')),
        ('/I' + (Join-Path $includeRoot 'ucrt')),
        ('/I' + (Join-Path $includeRoot 'shared')),
        ('/I' + (Join-Path $includeRoot 'um')),
        ('/I' + (Join-Path $includeRoot 'winrt')),
        ('/Fo' + (Join-Path $temporaryRoot 'launcher.obj')),
        ('/Fe' + $outputPath),
        $sourcePath,
        '/link', '/SUBSYSTEM:WINDOWS',
        ('/LIBPATH:' + (Join-Path $msvcRoot 'lib\x64')),
        ('/LIBPATH:' + (Join-Path $libraryRoot 'ucrt\x64')),
        ('/LIBPATH:' + (Join-Path $libraryRoot 'um\x64')),
        'kernel32.lib'
    )
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Native launcher compilation failed with exit code $LASTEXITCODE."
    }
    Get-Item -LiteralPath $outputPath
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}
