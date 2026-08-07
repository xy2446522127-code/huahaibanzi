param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$Thumbprint,
    [switch]$SkipTimestamp
)

$ErrorActionPreference = 'Stop'
$resolvedPath = [System.IO.Path]::GetFullPath($Path)
$normalizedThumbprint = ($Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
    throw "Release installer does not exist: $resolvedPath"
}
if ($normalizedThumbprint -notmatch '^[0-9A-F]{40}$') {
    throw 'Release signing certificate thumbprint must contain exactly 40 hexadecimal characters.'
}

$certificate = Get-Item -LiteralPath ('Cert:\CurrentUser\My\' + $normalizedThumbprint) -ErrorAction SilentlyContinue
if ($null -eq $certificate -or -not $certificate.HasPrivateKey) {
    throw "Release signing certificate with a private key was not found in Cert:\CurrentUser\My: $normalizedThumbprint"
}
if ($certificate.NotAfter -le (Get-Date)) {
    throw "Release signing certificate is expired: $normalizedThumbprint"
}

$signTool = Get-ChildItem -Path (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Recurse -Filter signtool.exe -ErrorAction Stop |
    Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($signTool)) {
    throw 'Windows SDK x64 signtool.exe was not found.'
}

$arguments = @('sign', '/sha1', $normalizedThumbprint, '/fd', 'SHA256')
if (-not $SkipTimestamp) {
    $arguments += @('/tr', 'http://timestamp.digicert.com', '/td', 'SHA256')
}
$arguments += $resolvedPath
& $signTool @arguments | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Authenticode signing failed with exit code $LASTEXITCODE"
}

$signature = Get-AuthenticodeSignature -LiteralPath $resolvedPath
$actualThumbprint = if ($null -eq $signature.SignerCertificate) { '' } else { $signature.SignerCertificate.Thumbprint }
if ($actualThumbprint -ne $normalizedThumbprint) {
    throw 'Authenticode signing completed with an unexpected publisher certificate.'
}

[pscustomobject]@{
    Path = $resolvedPath
    Thumbprint = $normalizedThumbprint
    Timestamped = -not $SkipTimestamp
} | ConvertTo-Json -Compress
