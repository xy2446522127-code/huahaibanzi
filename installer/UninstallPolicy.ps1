# 计算唯一允许卸载的当前用户安装目录。
function Get-HuahaiExpectedInstallRoot {
    param([Parameter(Mandatory = $true)][string]$LocalAppData)

    return [System.IO.Path]::GetFullPath((Join-Path $LocalAppData 'Programs\HuahaiClipboard')).TrimEnd('\')
}

# 只接受与标准安装目录完全一致的路径，拒绝相似目录或父子目录。
function Test-HuahaiInstallRoot {
    param(
        [Parameter(Mandatory = $true)][string]$InstallRoot,
        [Parameter(Mandatory = $true)][string]$LocalAppData,
        [AllowNull()][string]$ExpectedInstallRoot
    )

    if ([string]::IsNullOrWhiteSpace($ExpectedInstallRoot)) {
        $ExpectedInstallRoot = Get-HuahaiExpectedInstallRoot -LocalAppData $LocalAppData
    }

    $actualFull = [System.IO.Path]::GetFullPath($InstallRoot)
    $expectedFull = [System.IO.Path]::GetFullPath($ExpectedInstallRoot)
    $actualRoot = [System.IO.Path]::GetPathRoot($actualFull)
    $expectedRoot = [System.IO.Path]::GetPathRoot($expectedFull)
    if ([string]::Equals($actualFull, $actualRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
        [string]::Equals($expectedFull, $expectedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    $actual = $actualFull.TrimEnd('\')
    $expected = $expectedFull.TrimEnd('\')
    return [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)
}

# 仅识别直接启动当前安装目录应用入口的 Run 值。
function Test-HuahaiRunValueTargetsInstallRoot {
    param(
        [AllowNull()][string]$RunValue,
        [Parameter(Mandatory = $true)][string]$InstallRoot
    )

    if ([string]::IsNullOrWhiteSpace($RunValue)) { return $false }
    $match = [regex]::Match($RunValue.Trim(), '^(?:"(?<path>[^"]+)"|(?<path>\S+))')
    if (-not $match.Success) { return $false }

    $actual = [System.IO.Path]::GetFullPath($match.Groups['path'].Value)
    $expected = [System.IO.Path]::GetFullPath((Join-Path $InstallRoot 'HuahaiClipboard.exe'))
    return [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)
}

# 缺失的 Run 键或值按未配置处理，避免静默卸载因非终止错误中断。
function Get-HuahaiRunValue {
    param(
        [Parameter(Mandatory = $true)][string]$RunKeyPath,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $properties = Get-ItemProperty -LiteralPath $RunKeyPath -ErrorAction SilentlyContinue
    if ($null -eq $properties) { return $null }
    $property = $properties.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return [string]$property.Value
}
