param([string]$ExpectedVersion)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$versionXml = Get-Content (Join-Path $repoRoot 'Version.props')
$version = [string]$versionXml.Project.PropertyGroup.Version
$fileVersion = [string]$versionXml.Project.PropertyGroup.FileVersion
$assemblyVersion = [string]$versionXml.Project.PropertyGroup.AssemblyVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version.props must contain a strict major.minor.patch Version.' }
$expectedFourPart = "$version.0"
if ($fileVersion -ne $expectedFourPart) { throw "FileVersion must be $expectedFourPart." }
if ($assemblyVersion -ne $expectedFourPart) { throw "AssemblyVersion must be $expectedFourPart." }
if ($ExpectedVersion -and $version -ne $ExpectedVersion) { throw "Expected Version $ExpectedVersion but found $version." }

$projectFiles = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -File -Include *.csproj | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
foreach ($project in $projectFiles) {
    $text = Get-Content $project.FullName -Raw
    if ($text -match '<Version>|<FileVersion>|<AssemblyVersion>|<InformationalVersion>') { throw "Independent version found in $($project.FullName)" }
}
$production = Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -File -Include *.cs,*.xaml,*.csproj | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
foreach ($file in $production) {
    if ((Get-Content $file.FullName -Raw) -match [regex]::Escape($version)) { throw "Hard-coded canonical version found in production file: $($file.FullName)" }
}
$guide = Get-Content (Join-Path $repoRoot 'docs\PublicCloudDownloader-README.txt') -Raw
if ($guide -notmatch '\{\{VERSION\}\}') { throw 'Packaged guide must use {{VERSION}}.' }
$installer = Get-Content (Join-Path $repoRoot 'installer\PublicCloudDownloader.iss') -Raw
if ($installer -notmatch '#ifndef AppVersion') { throw 'Installer must require AppVersion.' }
$package = Get-Content (Join-Path $repoRoot 'scripts\package.ps1') -Raw
if ($package -notmatch 'Version.props' -or $package -notmatch 'DAppVersion') { throw 'Packaging must derive and pass the canonical version.' }
Write-Output "Version test passed: $version"
