$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'dist'))
$publishDir = [System.IO.Path]::GetFullPath((Join-Path $distRoot 'PublicCloudDownloader'))
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))

function Assert-RepositoryChild([string]$path) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    $prefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolved.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Unsafe build path: $resolved" }
}
Assert-RepositoryChild $distRoot
Assert-RepositoryChild $artifacts
foreach ($path in @($distRoot, $artifacts)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force } }
New-Item -ItemType Directory -Path $publishDir, $artifacts -Force | Out-Null

[xml]$versionXml = Get-Content (Join-Path $repoRoot 'Version.props')
$version = [string]$versionXml.Project.PropertyGroup.Version
& (Join-Path $PSScriptRoot 'version-test.ps1')

dotnet publish (Join-Path $repoRoot 'src\PublicCloudDownloader.App\PublicCloudDownloader.App.csproj') --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$guide = (Get-Content (Join-Path $repoRoot 'docs\PublicCloudDownloader-README.txt') -Raw).Replace('{{VERSION}}', $version)
Set-Content -LiteralPath (Join-Path $publishDir 'README.txt') -Value $guide -Encoding utf8
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\PublicCloudDownloader.App\Assets\PublicCloudDownloader.ico') -Destination (Join-Path $publishDir 'PublicCloudDownloader.ico')
New-Item -ItemType Directory -Path (Join-Path $publishDir 'data'), (Join-Path $publishDir 'logs') -Force | Out-Null
$selfTest = Start-Process -FilePath (Join-Path $publishDir 'PublicCloudDownloader.exe') -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
if ($selfTest.ExitCode -ne 0) { throw "Published self-test failed with exit code $($selfTest.ExitCode)." }
& (Join-Path $PSScriptRoot 'release-test.ps1') -ReleaseDirectory $publishDir

$zipPath = Join-Path $artifacts "PublicCloudDownloader-v$version-win-x64.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($publishDir, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$iscc = (& (Join-Path $PSScriptRoot 'install-build-tools.ps1') | Select-Object -Last 1).Trim()
& $iscc "/DAppVersion=$version" "/DPublishDir=$publishDir" "/DOutputDir=$artifacts" (Join-Path $repoRoot 'installer\PublicCloudDownloader.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed with exit code $LASTEXITCODE." }

$installerPath = Join-Path $artifacts "PublicCloudDownloader-v$version-Setup.exe"
foreach ($required in @($zipPath, $installerPath)) { if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Expected artifact was not created: $required" } }
$hashLines = foreach ($file in @($installerPath, $zipPath)) { $hash = Get-FileHash -LiteralPath $file -Algorithm SHA256; "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($file))" }
Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Value $hashLines -Encoding ascii
Write-Output "Created release artifacts for $version in $artifacts"
