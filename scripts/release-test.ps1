param(
    [string]$ReleaseDirectory,
    [string]$ZipPath,
    [switch]$AllowRuntimeData
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Invoke-SelfTest([string]$root) {
    $exe = Join-Path $root 'PublicCloudDownloader.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
        throw "Missing application executable: $exe"
    }
    $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) { throw "Self-test failed in $root with exit code $($process.ExitCode)." }
}

function Test-Payload([string]$root, [bool]$allowRuntimeData) {
    $resolved = [System.IO.Path]::GetFullPath($root)
    if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "Release payload directory does not exist: $resolved"
    }

    $expectedNames = @(
        'PublicCloudDownloader.exe',
        'PublicCloudDownloader.ico',
        'README.txt',
        'THIRD-PARTY-NOTICES.md',
        'data',
        'logs'
    ) | Sort-Object
    $actualNames = @(Get-ChildItem -LiteralPath $resolved -Force | ForEach-Object Name | Sort-Object)
    if (Compare-Object $expectedNames $actualNames) { throw "Unexpected release root entries in $resolved." }

    foreach ($file in @('PublicCloudDownloader.exe', 'PublicCloudDownloader.ico', 'README.txt', 'THIRD-PARTY-NOTICES.md')) {
        if (-not (Test-Path -LiteralPath (Join-Path $resolved $file) -PathType Leaf)) {
            throw "Release payload is missing file: $file"
        }
    }
    [xml]$versionXml = Get-Content (Join-Path $repoRoot 'Version.props')
    $expectedFileVersion = ([string]$versionXml.Project.PropertyGroup.Version) + '.0'
    $actualFileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $resolved 'PublicCloudDownloader.exe')).FileVersion
    if ($actualFileVersion -ne $expectedFileVersion) {
        throw "Release executable version mismatch. Expected $expectedFileVersion but found $actualFileVersion."
    }
    foreach ($directory in @('data', 'logs')) {
        $path = Join-Path $resolved $directory
        if (-not (Test-Path -LiteralPath $path -PathType Container)) {
            throw "Release payload is missing directory: $directory"
        }
        if (-not $allowRuntimeData -and @(Get-ChildItem -LiteralPath $path -Force).Count -ne 0) {
            throw "Distributable runtime directory must be empty: $directory"
        }
    }
}

$sourceFiles = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
$retired = 'RcloneConfigService|AccountsWindow|SyncPreview|TransferMode\.Sync|rclone\.exe|OAuthClient|client_secret|UploadToCloud|CloudDestination'
foreach ($file in $sourceFiles) {
    if ((Get-Content $file.FullName -Raw) -match $retired) { throw "Retired feature identifier found in $($file.FullName)" }
}

$tempRoot = $null
try {
    if ($ReleaseDirectory) {
        Test-Payload ([System.IO.Path]::GetFullPath($ReleaseDirectory)) $AllowRuntimeData.IsPresent
    }

    if ($ZipPath) {
        $resolvedZip = [System.IO.Path]::GetFullPath($ZipPath)
        if (-not (Test-Path -LiteralPath $resolvedZip -PathType Leaf)) {
            throw "ZIP file does not exist: $resolvedZip"
        }
        if ([System.IO.Path]::GetExtension($resolvedZip) -ine '.zip') {
            throw "ZIP path must have a .zip extension: $resolvedZip"
        }

        $systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $tempRoot = Join-Path $systemTemp ("pcd-release-test-" + [Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
        Expand-Archive -LiteralPath $resolvedZip -DestinationPath $tempRoot
        Test-Payload $tempRoot $false
        Invoke-SelfTest $tempRoot
        Test-Payload $tempRoot $false
    }
} finally {
    if ($tempRoot) {
        $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
        $systemTemp = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
        $systemTempNormalized = $systemTemp.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $parent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetDirectoryName($resolvedTemp))
        $parentNormalized = $parent.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $basename = [System.IO.Path]::GetFileName($resolvedTemp)
        if ($parentNormalized -ine $systemTempNormalized -or $basename -notmatch '^pcd-release-test-[0-9a-f]{32}$') {
            throw "Refused unsafe cleanup path: $resolvedTemp"
        }
        if (Test-Path -LiteralPath $resolvedTemp) {
            Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
        }
    }
}
Write-Output 'Release tests passed.'
