$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $distRoot 'PublicCloudDownloader'))
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$stagingDir = $null

function Assert-RepositoryChild([string]$path) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    $prefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsafe build path: $resolved"
    }
}

function Assert-RuntimeDirectory([string]$path) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        throw "Runtime path must be a directory: $path"
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

function Remove-PublishGeneratedEntries([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { return }
    Get-ChildItem -LiteralPath $path -Force |
        Where-Object { $_.Name -notin @('data', 'logs') } |
        Remove-Item -Recurse -Force
}

function Copy-DistributionPayload([string]$source, [string]$destination) {
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($name in @('PublicCloudDownloader.exe', 'PublicCloudDownloader.ico', 'README.txt', 'THIRD-PARTY-NOTICES.md')) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $destination $name)
    }
    New-Item -ItemType Directory -Path (Join-Path $destination 'data'), (Join-Path $destination 'logs') -Force | Out-Null
}

function Write-DistributionZip([string]$source, [string]$destination) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream = [System.IO.File]::Open(
        $destination,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            foreach ($name in @('PublicCloudDownloader.exe', 'PublicCloudDownloader.ico', 'README.txt', 'THIRD-PARTY-NOTICES.md')) {
                $entry = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
                $sourceStream = [System.IO.File]::OpenRead((Join-Path $source $name))
                try {
                    $entryStream = $entry.Open()
                    try {
                        $sourceStream.CopyTo($entryStream)
                    } finally {
                        $entryStream.Dispose()
                    }
                } finally {
                    $sourceStream.Dispose()
                }
            }
            foreach ($name in @('data/', 'logs/')) {
                $archive.CreateEntry($name) | Out-Null
            }
        } finally {
            $archive.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

function Assert-ArtifactSet([string]$root, [string[]]$expectedNames) {
    $actualNames = @(Get-ChildItem -LiteralPath $root -Force | ForEach-Object Name | Sort-Object)
    $sortedExpected = @($expectedNames | Sort-Object)
    if (Compare-Object $sortedExpected $actualNames) {
        throw 'Artifact directory contains unexpected entries.'
    }
}

Assert-RepositoryChild $distRoot
Assert-RepositoryChild $publishDir
Assert-RepositoryChild $artifacts
foreach ($path in @($distRoot, $publishDir, $artifacts)) {
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        throw "Build path must be a directory: $path"
    }
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

try {
    Get-ChildItem -LiteralPath $artifacts -Force | Remove-Item -Recurse -Force
    Remove-PublishGeneratedEntries $publishDir
    Assert-RuntimeDirectory (Join-Path $publishDir 'data')
    Assert-RuntimeDirectory (Join-Path $publishDir 'logs')

    [xml]$versionXml = Get-Content (Join-Path $repoRoot 'Version.props')
    $version = [string]$versionXml.Project.PropertyGroup.Version
    & (Join-Path $PSScriptRoot 'version-test.ps1') -ExpectedVersion $version

    dotnet publish (Join-Path $repoRoot 'src\PublicCloudDownloader.App\PublicCloudDownloader.App.csproj') --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

    $guide = (Get-Content (Join-Path $repoRoot 'docs\PublicCloudDownloader-README.txt') -Raw).Replace('{{VERSION}}', $version)
    Set-Content -LiteralPath (Join-Path $publishDir 'README.txt') -Value $guide -Encoding utf8
    Copy-Item -LiteralPath (Join-Path $repoRoot 'src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico') -Destination (Join-Path $publishDir 'PublicCloudDownloader.ico')
    Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $publishDir 'THIRD-PARTY-NOTICES.md')
    Assert-RuntimeDirectory (Join-Path $publishDir 'data')
    Assert-RuntimeDirectory (Join-Path $publishDir 'logs')

    $selfTest = Start-Process -FilePath (Join-Path $publishDir 'PublicCloudDownloader.exe') -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
    if ($selfTest.ExitCode -ne 0) { throw "Published self-test failed with exit code $($selfTest.ExitCode)." }

    & (Join-Path $PSScriptRoot 'release-test.ps1') -ReleaseDirectory $publishDir -AllowRuntimeData

    do {
        $stagingDir = Join-Path $distRoot ('.zip-staging-' + [Guid]::NewGuid().ToString('N'))
    } while (Test-Path -LiteralPath $stagingDir)
    Assert-RepositoryChild $stagingDir
    Copy-DistributionPayload $publishDir $stagingDir

    & (Join-Path $PSScriptRoot 'release-test.ps1') -ReleaseDirectory $stagingDir

    $zipName = "PublicCloudDownloader-v$version-win-x64.zip"
    $zipPath = Join-Path $artifacts $zipName
    Write-DistributionZip $stagingDir $zipPath
    if (-not (Test-Path -LiteralPath $zipPath -PathType Leaf)) { throw "Expected ZIP was not created: $zipPath" }

    & (Join-Path $PSScriptRoot 'release-test.ps1') -ZipPath $zipPath

    Assert-ArtifactSet $artifacts @($zipName)
    $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumLine = "$zipHash  $zipName"
    Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Value $checksumLine -Encoding ascii

    @(
        "Version: $version"
        'Publish build: PASS'
        'Published self-test: PASS (exit code 0)'
        'Published payload validation: PASS'
        'ZIP creation: PASS'
        'Extracted ZIP payload validation: PASS'
        'Extracted ZIP self-test: PASS (exit code 0)'
        'Artifact-set validation: PASS'
        "SHA-256: $checksumLine"
    ) | Set-Content -LiteralPath (Join-Path $artifacts 'verification.txt') -Encoding ascii

    Assert-ArtifactSet $artifacts @($zipName, 'SHA256SUMS.txt', 'verification.txt')
    Write-Output "Created ZIP release artifacts for $version in $artifacts"
} finally {
    if ($stagingDir) {
        $resolvedStaging = [System.IO.Path]::GetFullPath($stagingDir)
        $resolvedDist = [System.IO.Path]::GetFullPath($distRoot)
        $distNormalized = $resolvedDist.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $parent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetDirectoryName($resolvedStaging))
        $parentNormalized = $parent.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $basename = [System.IO.Path]::GetFileName($resolvedStaging)
        if ($parentNormalized -ine $distNormalized -or $basename -notmatch '^\.zip-staging-[0-9a-f]{32}$') {
            throw "Refused unsafe staging cleanup path: $resolvedStaging"
        }
        if (Test-Path -LiteralPath $resolvedStaging) {
            Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
        }
    }
}
