$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$policySource = Join-Path $projectRoot 'src\HuahaiClipboard.Core\Services\InstallerPublisherSignaturePolicy.cs'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboard.PublisherSignatureTests.' + [guid]::NewGuid().ToString('N'))
$probeSource = Join-Path $fixtureRoot 'PublisherSignatureProbe.cs'
$probeExe = Join-Path $fixtureRoot 'PublisherSignatureProbe.exe'
$signedExe = Join-Path $fixtureRoot 'signed-fixture.exe'
$tamperedExe = Join-Path $fixtureRoot 'tampered-fixture.exe'
$unsignedExe = Join-Path $fixtureRoot 'unsigned-fixture.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$signTool = Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Recurse -Filter signtool.exe -ErrorAction Stop |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
$certificate = $null

function Invoke-Probe {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Thumbprint,
        [Parameter(Mandatory = $true)][bool]$ExpectSuccess
    )

    $output = $null
    $succeeded = $false
    try {
        $output = & $probeExe $Path $Thumbprint 2>&1
        $succeeded = $LASTEXITCODE -eq 0
    }
    catch {
        $output = $_.Exception.Message
        $succeeded = $false
    }
    if ($succeeded -ne $ExpectSuccess) {
        throw "Publisher signature probe result was unexpected for $Path. Exit=$LASTEXITCODE Output=$output"
    }
}

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    @'
using System;
using HuahaiClipboard.Core.Services;

internal static class PublisherSignatureProbe
{
    private static int Main(string[] args)
    {
        try
        {
            InstallerPublisherSignaturePolicy.Verify(args[0], args[1]);
            Console.WriteLine("passed");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetType().Name + ": " + error.Message);
            return 7;
        }
    }
}
'@ | Set-Content -LiteralPath $probeSource -Encoding UTF8

    & $csc /nologo /target:exe /out:$probeExe $policySource $probeSource
    if ($LASTEXITCODE -ne 0) { throw "Publisher signature probe compilation failed with exit code $LASTEXITCODE" }

    Copy-Item -LiteralPath $probeExe -Destination $signedExe
    Copy-Item -LiteralPath $probeExe -Destination $unsignedExe
    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject ('CN=HuahaiClipboard Publisher Signature Test ' + [guid]::NewGuid().ToString('N')) `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddDays(1)

    & $signTool sign /sha1 $certificate.Thumbprint /fd SHA256 $signedExe | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Signing the publisher test fixture failed with exit code $LASTEXITCODE" }

    $signedCertificate = (Get-AuthenticodeSignature -LiteralPath $signedExe).SignerCertificate
    if ($null -eq $signedCertificate -or $signedCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw "The signed fixture certificate does not match the requested test certificate."
    }

    Invoke-Probe -Path $signedExe -Thumbprint $certificate.Thumbprint -ExpectSuccess $true
    Invoke-Probe -Path $signedExe -Thumbprint ('0' * 40) -ExpectSuccess $false
    Invoke-Probe -Path $unsignedExe -Thumbprint $certificate.Thumbprint -ExpectSuccess $false

    Copy-Item -LiteralPath $signedExe -Destination $tamperedExe
    [System.IO.File]::AppendAllText($tamperedExe, 'tampered')
    Invoke-Probe -Path $tamperedExe -Thumbprint $certificate.Thumbprint -ExpectSuccess $false

    [pscustomobject]@{
        Status = 'passed'
        SignedAccepted = 1
        WrongPublisherRejected = 1
        UnsignedRejected = 1
        TamperedRejected = 1
    } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $certificate) {
        Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $certificate.Thumbprint) -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [System.IO.Path]::GetFullPath($fixtureRoot)
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^HuahaiClipboard\.PublisherSignatureTests\.[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected publisher signature fixture path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
