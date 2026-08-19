param(
    [Parameter(Mandatory=$true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location $repoRoot
$tag = "v$Version"
$zip = "artifacts/PublicCloudDownloader-v$Version-win-x64.zip"
$assets = @($zip, 'artifacts/SHA256SUMS.txt', 'artifacts/verification.txt')

if ((git branch --show-current).Trim() -ne 'main') { throw 'Release publishing is allowed only from main.' }
if ((git status --porcelain).Count -ne 0) { throw 'Release publishing requires a clean working tree.' }

gh auth status | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI authentication is required.' }

git fetch origin main --tags | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'git fetch failed.' }
$local = (git rev-parse HEAD).Trim()
$remote = (git rev-parse origin/main).Trim()
if ($local -ne $remote) { throw "main must match origin/main before release. local=$local remote=$remote" }

$repo = gh repo view arkiez/PublicCloudDownloader --json visibility,isPrivate | ConvertFrom-Json
if ($repo.visibility -ne 'PUBLIC' -or $repo.isPrivate) { throw 'Repository must be PUBLIC before publishing a release.' }

& (Join-Path $PSScriptRoot 'version-test.ps1') -ExpectedVersion $Version
if ($LASTEXITCODE -ne 0) { throw 'Version test failed.' }
dotnet test (Join-Path $repoRoot 'tests\PublicCloudDownloader.Tests\PublicCloudDownloader.Tests.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Full test suite failed.' }

& (Join-Path $PSScriptRoot 'package.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Package validation failed.' }

& (Join-Path $PSScriptRoot 'security-scan-public.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Public repository safety scan failed.' }

foreach ($asset in $assets) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $asset) -PathType Leaf)) { throw "Missing release asset: $asset" }
}

if (git tag --list $tag) { throw "Local tag already exists: $tag" }
$remoteTag = git ls-remote --tags origin "refs/tags/$tag"
if ($remoteTag) { throw "Remote tag already exists: $tag" }
$existingRelease = @(gh release list --repo arkiez/PublicCloudDownloader --json tagName --limit 100 | ConvertFrom-Json | Where-Object { $_.tagName -eq $tag })`r`nif ($existingRelease.Count -gt 0) { throw "GitHub Release already exists: $tag" }

git tag -a $tag -m "Public Cloud Downloader $tag"
if ($LASTEXITCODE -ne 0) { throw 'Tag creation failed.' }
git push origin $tag
if ($LASTEXITCODE -ne 0) { throw 'Tag push failed.' }

gh release create $tag @assets --repo arkiez/PublicCloudDownloader --title "Public Cloud Downloader $tag" --generate-notes --latest --verify-tag
if ($LASTEXITCODE -ne 0) { throw "GitHub Release creation failed. Tag $tag remains published for inspection." }

Write-Output "Published Public Cloud Downloader $tag from $local"
