$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$signingScript = Join-Path $projectRoot 'installer\Sign-ReleaseInstaller.ps1'
$fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) ('HuahaiClipboard.ReleaseSigningTests.' + [guid]::NewGuid().ToString('N'))
$fixtureSource = Join-Path $fixtureRoot 'SignedFixture.cs'
$fixtureExe = Join-Path $fixtureRoot 'SignedFixture.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$certificate = $null

try {
    New-Item -ItemType Directory -Path $fixtureRoot -Force | Out-Null
    'internal static class SignedFixture { private static int Main() { return 0; } }' |
        Set-Content -LiteralPath $fixtureSource -Encoding ASCII
    & $csc /nologo /target:winexe /out:$fixtureExe $fixtureSource
    if ($LASTEXITCODE -ne 0) { throw "Release signing fixture compilation failed with exit code $LASTEXITCODE" }

    $certificate = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject ('CN=HuahaiClipboard Release Signing Test ' + [guid]::NewGuid().ToString('N')) `
        -CertStoreLocation 'Cert:\CurrentUser\My' `
        -HashAlgorithm SHA256 `
        -KeyAlgorithm RSA `
        -KeyLength 2048 `
        -NotAfter (Get-Date).AddDays(1)

    & $signingScript -Path $fixtureExe -Thumbprint $certificate.Thumbprint -SkipTimestamp
    $signature = Get-AuthenticodeSignature -LiteralPath $fixtureExe
    if ($null -eq $signature.SignerCertificate) {
        throw 'The release signing policy left the fixture unsigned.'
    }
    if ($signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) {
        throw 'The release signing policy used an unexpected publisher certificate.'
    }

    [pscustomobject]@{ Status = 'passed'; PublisherPinned = 1 } | ConvertTo-Json -Compress
}
finally {
    if ($null -ne $certificate) {
        Remove-Item -LiteralPath ('Cert:\CurrentUser\My\' + $certificate.Thumbprint) -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolved = [System.IO.Path]::GetFullPath($fixtureRoot)
        $expectedParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $resolved.StartsWith($expectedParent, [System.StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path -Leaf $resolved) -notmatch '^HuahaiClipboard\.ReleaseSigningTests\.[0-9a-f]{32}$') {
            throw "Refusing to clean unexpected release signing fixture path: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
