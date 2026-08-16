param([string]$ArtifactsDirectory)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string]$message) { $failures.Add($message) }
function Read-RepositoryFile([string]$relativePath) {
    $path = Join-Path $repoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Failure "Missing required file: $relativePath"
        return ''
    }
    return Get-Content -LiteralPath $path -Raw
}

[xml]$versionXml = Get-Content -LiteralPath (Join-Path $repoRoot 'Version.props')
$version = [string]$versionXml.Project.PropertyGroup.Version
$zipName = "PublicCloudDownloader-v$version-win-x64.zip"

$retiredFiles = @(
    (Join-Path 'installer' ('PublicCloudDownloader' + '.iss')),
    (Join-Path 'scripts' ('install-' + 'build-tools.ps1'))
)
foreach ($retired in $retiredFiles) {
    if (Test-Path -LiteralPath (Join-Path $repoRoot $retired)) {
        Add-Failure "Retired installer file still exists: $retired"
    }
}

$activeFiles = @(
    'scripts\package.ps1',
    'scripts\release-test.ps1',
    'scripts\version-test.ps1',
    'README.md',
    'docs\PublicCloudDownloader-README.txt',
    'docs\requirements\2026-08-14-versioning-and-release-requirements.md',
    'docs\requirements\2026-08-14-octicons-ui-requirements.md'
)
$retiredPattern = @(
    ('install-' + 'build-tools')
    ('PublicCloudDownloader' + '\.iss')
    ('Installer' + 'Path')
    ('DApp' + 'Version')
    ('\b' + 'ISCC' + '\b')
    ('Setup' + '\.exe')
    ('per-user Windows ' + 'installer')
    ('ZIP and ' + 'installer')
) -join '|'
foreach ($relativePath in $activeFiles) {
    $text = Read-RepositoryFile $relativePath
    if ($text -match $retiredPattern) {
        Add-Failure "Active installer reference remains in $relativePath"
    }
}

$package = Read-RepositoryFile 'scripts\package.ps1'
foreach ($requiredPattern in @('Version\.props', '-win-x64\.zip', 'SHA256SUMS\.txt', 'verification\.txt', 'release-test\.ps1', '-ZipPath', '--self-test')) {
    if ($package -notmatch $requiredPattern) {
        Add-Failure "package.ps1 is missing required pattern: $requiredPattern"
    }
}
$stalePowerShellExitPattern = '(?ms)&\s*\(Join-Path[^\r\n]*(version-test|release-test)\.ps1[^\r\n]*\)\s*[^\r\n]*\r?\n\s*if\s*\(\s*\$LASTEXITCODE'
if ($package -match $stalePowerShellExitPattern) {
    Add-Failure 'package.ps1 must not gate PowerShell script invocations on stale $LASTEXITCODE.'
}

if ($ArtifactsDirectory) {
    $artifactRoot = [System.IO.Path]::GetFullPath($ArtifactsDirectory)
    if (-not (Test-Path -LiteralPath $artifactRoot -PathType Container)) {
        Add-Failure "Artifacts directory does not exist: $artifactRoot"
    } else {
        $expectedNames = @($zipName, 'SHA256SUMS.txt', 'verification.txt') | Sort-Object
        $actualNames = @(Get-ChildItem -LiteralPath $artifactRoot -Force | ForEach-Object Name | Sort-Object)
        if (Compare-Object $expectedNames $actualNames) {
            Add-Failure 'Artifact directory must contain exactly the ZIP, SHA256SUMS.txt, and verification.txt.'
        }

        $zipPath = Join-Path $artifactRoot $zipName
        if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
            $zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
            $lines = @()
            if (Test-Path -LiteralPath $checksumPath -PathType Leaf) {
                $lines = @(Get-Content -LiteralPath $checksumPath | Where-Object { $_ })
            }
            $expectedLine = "$zipHash  $zipName"
            if ($lines.Count -ne 1 -or $lines[0] -cne $expectedLine) {
                Add-Failure 'SHA256SUMS.txt must contain one exact lowercase ZIP checksum.'
            }
        }

        $verificationPath = Join-Path $artifactRoot 'verification.txt'
        $verificationLines = @()
        if (Test-Path -LiteralPath $verificationPath -PathType Leaf) {
            $verificationLines = @(Get-Content -LiteralPath $verificationPath)
        }
        if (Test-Path -LiteralPath $zipPath -PathType Leaf) {
            $expectedVerificationLines = @(
                "Version: $version"
                'Publish build: PASS'
                'Published self-test: PASS (exit code 0)'
                'Published payload validation: PASS'
                'ZIP creation: PASS'
                'Extracted ZIP payload validation: PASS'
                'Extracted ZIP self-test: PASS (exit code 0)'
                'Artifact-set validation: PASS'
                "SHA-256: $zipHash  $zipName"
            )
            $verificationMatches = $verificationLines.Count -eq $expectedVerificationLines.Count
            if ($verificationMatches) {
                for ($index = 0; $index -lt $expectedVerificationLines.Count; $index++) {
                    if ($verificationLines[$index] -cne $expectedVerificationLines[$index]) {
                        $verificationMatches = $false
                        break
                    }
                }
            }
            if (-not $verificationMatches) {
                Add-Failure 'verification.txt must contain the exact ordered release lines, including the ZIP hash and filename.'
            }
        } elseif ($verificationLines.Count -eq 0) {
            Add-Failure 'verification.txt is missing.'
        }
    }
}

if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output 'ZIP-only release contract passed.'
