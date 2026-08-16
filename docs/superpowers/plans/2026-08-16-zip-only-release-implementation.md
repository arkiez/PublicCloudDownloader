# ZIP-Only Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce Public Cloud Downloader 1.1.2 as one verified portable ZIP, remove every active Inno Setup path, preserve local runtime data/logs, and run the built-in self-test before compression and after final-ZIP extraction.

**Architecture:** Keep `dist/PublicCloudDownloader` as the local runtime publish directory and preserve its existing `data` and `logs` trees. Assemble the distributable from an explicit four-file allowlist in a temporary repository-local staging directory with new empty runtime directories, then validate and self-test the extracted ZIP. Treat `artifacts` as generated output containing one ZIP plus checksum and verification metadata.

**Tech Stack:** PowerShell 5.1-compatible scripts, .NET 8, WPF, xUnit, `System.IO.Compression.ZipFile`

## Global Constraints

- Target version is exactly `1.1.2`, sourced only from `Version.props`.
- Produce only `PublicCloudDownloader-v1.1.2-win-x64.zip` as the distributable application package.
- Keep `SHA256SUMS.txt` and `verification.txt` as release metadata.
- Do not create, invoke, install, update, or uninstall Inno Setup or any Setup EXE.
- Preserve every pre-existing file and its content under `dist/PublicCloudDownloader/data` and `dist/PublicCloudDownloader/logs`.
- Put empty `data` and `logs` directories in the ZIP; never include local runtime contents.
- Run `PublicCloudDownloader.exe --self-test` exactly once from the publish directory and once from an extracted final ZIP.
- Do not add a separate self-test executable or user-facing self-test script.
- Preserve the worktree's existing Octicons, XAML, notice, and version edits; patch shared release files surgically.
- Do not touch the main worktree or its untracked icon.
- Do not publish, upload, sign, or externally distribute artifacts.

## File Map

- Create `scripts/zip-only-release-test.ps1`: source and generated-artifact contract test.
- Modify `scripts/package.ps1`: ZIP-only orchestration, runtime preservation, staging, two self-tests, hashing, and evidence.
- Modify `scripts/release-test.ps1`: local-runtime and clean-distribution payload validation; extracted-ZIP self-test.
- Modify `scripts/version-test.ps1`: canonical ZIP-version checks without installer metadata.
- Modify `README.md`: ZIP-only distribution/build instructions.
- Modify `docs/PublicCloudDownloader-README.txt`: portable removal instructions.
- Modify `docs/requirements/2026-08-14-versioning-and-release-requirements.md`: current ZIP-only release gate and runtime-preservation contract.
- Modify `docs/requirements/2026-08-14-octicons-ui-requirements.md`: require the notice in the ZIP only.
- Delete `installer/PublicCloudDownloader.iss`.
- Delete `scripts/install-build-tools.ps1`.
- Leave `src/PublicCloudDownloader.App/App.xaml.cs`, `src/PublicCloudDownloader.Infrastructure/Runtime/AppSelfTest.cs`, and their existing test unchanged.

---

### Task 1: Add a failing ZIP-only release contract

**Files:**

- Create: `scripts/zip-only-release-test.ps1`
- Inspect: `Version.props`
- Inspect: `scripts/package.ps1`
- Inspect: `scripts/release-test.ps1`
- Inspect: `scripts/version-test.ps1`

**Interfaces:**

- Consumes: repository root inferred from `$PSScriptRoot`; optional `-ArtifactsDirectory`.
- Produces: exit `0` and `ZIP-only release contract passed.` only when source and optional artifacts satisfy the approved contract.

- [ ] **Step 1: Create the contract test before changing production scripts**

Implement this structure in `scripts/zip-only-release-test.ps1`:

```powershell
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

foreach ($retired in @('installer\PublicCloudDownloader.iss', 'scripts\install-build-tools.ps1')) {
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
$retiredPattern = 'install-build-tools|PublicCloudDownloader\.iss|InstallerPath|DAppVersion|\bISCC\b|Setup\.exe|per-user Windows installer|ZIP and installer'
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
            $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $checksumPath = Join-Path $artifactRoot 'SHA256SUMS.txt'
            $lines = if (Test-Path -LiteralPath $checksumPath) {
                @(Get-Content -LiteralPath $checksumPath | Where-Object { $_ })
            } else { @() }
            $expectedLine = "$hash  $zipName"
            if ($lines.Count -ne 1 -or $lines[0] -cne $expectedLine) {
                Add-Failure 'SHA256SUMS.txt must contain one exact lowercase ZIP checksum.'
            }
        }

        $verificationPath = Join-Path $artifactRoot 'verification.txt'
        $verification = if (Test-Path -LiteralPath $verificationPath) {
            Get-Content -LiteralPath $verificationPath -Raw
        } else { '' }
        foreach ($label in @(
            'Version: 1.1.2',
            'Publish build: PASS',
            'Published self-test: PASS (exit code 0)',
            'Published payload validation: PASS',
            'ZIP creation: PASS',
            'Extracted ZIP payload validation: PASS',
            'Extracted ZIP self-test: PASS (exit code 0)',
            'Artifact-set validation: PASS',
            'SHA-256:'
        )) {
            if ($verification -notmatch [regex]::Escape($label)) {
                Add-Failure "verification.txt is missing: $label"
            }
        }
    }
}

if ($failures.Count -gt 0) { throw ($failures -join [Environment]::NewLine) }
Write-Output 'ZIP-only release contract passed.'
```

- [ ] **Step 2: Run the test and capture the expected RED result**

```powershell
./scripts/zip-only-release-test.ps1 -ArtifactsDirectory ./artifacts
```

Expected: non-zero exit listing both retired files, installer references, the stale `*-Setup.exe`, and missing `verification.txt`. If it fails because of a syntax error instead, fix the test and rerun until it fails for those missing ZIP-only behaviors.

---

### Task 2: Replace installer orchestration with safe ZIP staging

**Files:**

- Modify: `scripts/package.ps1`
- Modify: `scripts/release-test.ps1`
- Modify: `scripts/version-test.ps1`
- Delete: `installer/PublicCloudDownloader.iss`
- Delete: `scripts/install-build-tools.ps1`
- Test: `scripts/zip-only-release-test.ps1`

**Interfaces:**

- `release-test.ps1 -ReleaseDirectory <path> -AllowRuntimeData` validates a local publish payload without inspecting or deleting preserved runtime contents and without starting a self-test.
- `release-test.ps1 -ReleaseDirectory <path>` validates an allowlisted distributable payload with empty runtime directories and without starting a self-test.
- `release-test.ps1 -ZipPath <path>` safely extracts, validates, runs one extracted self-test, validates again, and removes its own temporary directory.
- `package.ps1` produces one ZIP and two metadata files in `artifacts`.

- [ ] **Step 1: Replace `release-test.ps1` with two explicit payload contracts**

Keep the retired-source scan, remove `InstallerPath` and all install/uninstall logic, and implement these signatures and checks:

```powershell
param(
    [string]$ReleaseDirectory,
    [string]$ZipPath,
    [switch]$AllowRuntimeData
)

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
```

For `-ZipPath`, require a real ZIP file, create `pcd-release-test-<32 lowercase hex>` directly under `[System.IO.Path]::GetTempPath()`, extract once, call `Test-Payload $root $false`, call `Invoke-SelfTest`, call `Test-Payload $root $false` again, and remove only that validated child in `finally`. Validate both the exact system-temp parent and basename regex `^pcd-release-test-[0-9a-f]{32}$` before recursive removal.

- [ ] **Step 2: Make `version-test.ps1` validate ZIP-derived versioning**

Remove `.iss` and `DAppVersion` reads. Replace the package assertion with:

```powershell
$package = Get-Content (Join-Path $repoRoot 'scripts\package.ps1') -Raw
if ($package -notmatch 'Version\.props' -or $package -notmatch '\$version-win-x64\.zip') {
    throw 'Packaging must derive the versioned ZIP filename from Version.props.'
}
```

Retain the strict three-part version, four-part file/assembly versions, no independent project versions, no hard-coded production version, and packaged README token checks.

- [ ] **Step 3: Rewrite `package.ps1` around preservation and allowlisted staging**

Retain `Assert-RepositoryChild`. Add helpers with these responsibilities:

```powershell
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
```

Use this order:

1. Resolve and assert `dist`, `dist/PublicCloudDownloader`, `artifacts`, and a unique `dist/.zip-staging-<32 hex>`.
2. Clear only children of `artifacts`.
3. Remove only non-runtime entries from the publish directory.
4. Ensure publish `data` and `logs` are directories without enumerating or altering their contents.
5. Read version and run `version-test.ps1 -ExpectedVersion $version`.
6. Run the existing self-contained single-file `dotnet publish` command into the publish directory.
7. Write/copy `README.txt`, `PublicCloudDownloader.ico`, and `THIRD-PARTY-NOTICES.md`.
8. Run the publish EXE with `--self-test`; require exit `0`.
9. Run `release-test.ps1 -ReleaseDirectory $publishDir -AllowRuntimeData`.
10. Create the staging directory, copy exactly the four allowlisted files, and create empty `data`/`logs`.
11. Run `release-test.ps1 -ReleaseDirectory $zipStage`.
12. Create the version-derived ZIP from staging and require the file to exist.
13. Run `release-test.ps1 -ZipPath $zipPath` for the sole extracted-ZIP self-test.
14. Require the artifact directory to contain exactly the ZIP before metadata creation.
15. Hash only the ZIP and write exactly one lowercase checksum line with two spaces before the filename.
16. Write these stable verification lines:

```text
Version: 1.1.2
Publish build: PASS
Published self-test: PASS (exit code 0)
Published payload validation: PASS
ZIP creation: PASS
Extracted ZIP payload validation: PASS
Extracted ZIP self-test: PASS (exit code 0)
Artifact-set validation: PASS
SHA-256: <lowercase hash>  PublicCloudDownloader-v1.1.2-win-x64.zip
```

17. In `finally`, recursively remove only a staging path whose parent resolves exactly to `dist` and whose basename matches `^\.zip-staging-[0-9a-f]{32}$`.

- [ ] **Step 4: Remove repository installer files**

```powershell
git rm -- installer/PublicCloudDownloader.iss scripts/install-build-tools.ps1
```

Do not invoke or uninstall the workstation's Inno Setup application.

- [ ] **Step 5: Run the source contract and verify GREEN**

```powershell
./scripts/zip-only-release-test.ps1
./scripts/version-test.ps1 -ExpectedVersion 1.1.2
```

Expected: `ZIP-only release contract passed.` and `Version test passed: 1.1.2`.

- [ ] **Step 6: Review and commit only release-script changes**

```powershell
git diff --check
git diff -- scripts/package.ps1 scripts/release-test.ps1 scripts/version-test.ps1 scripts/zip-only-release-test.ps1 installer/PublicCloudDownloader.iss scripts/install-build-tools.ps1
git add -- scripts/package.ps1 scripts/release-test.ps1 scripts/version-test.ps1 scripts/zip-only-release-test.ps1 installer/PublicCloudDownloader.iss scripts/install-build-tools.ps1
git commit -m "build: make releases zip-only"
```

Do not stage XAML, icon, notice, requirement, or version worktree changes in this commit.

---

### Task 3: Update active ZIP-only documentation

**Files:**

- Modify: `README.md`
- Modify: `docs/PublicCloudDownloader-README.txt`
- Modify: `docs/requirements/2026-08-14-versioning-and-release-requirements.md`
- Modify: `docs/requirements/2026-08-14-octicons-ui-requirements.md`
- Test: `scripts/zip-only-release-test.ps1`

**Interfaces:**

- Consumes: ZIP-only script names and verification behavior from Task 2.
- Produces: active contributor/user documentation with no installer promise or dependency.

- [ ] **Step 1: Update active docs with exact ZIP-only behavior**

Apply these content changes:

- `README.md`: replace “Portable ZIP and a per-user Windows installer” with “Portable ZIP for Windows x64”; remove the uninstall paragraph; change build prerequisites to “Requires the .NET 8 SDK”; retain the three build commands.
- `docs/PublicCloudDownloader-README.txt`: replace `UNINSTALL` with `PORTABLE REMOVAL`, instruct the user to close the app and delete its complete folder, and state that downloaded files elsewhere are not deleted.
- Versioning requirements: replace installer metadata with release metadata; define ZIP as the sole distributable; require preserved local publish data/logs, empty ZIP data/logs, both self-tests, extracted-ZIP validation, one checksum entry, and `verification.txt`; label the 1.1.1 section as a historical delivery record.
- Octicons requirements: replace “Packaged ZIP and installer payloads contain `THIRD-PARTY-NOTICES.md`” with “The portable ZIP payload contains `THIRD-PARTY-NOTICES.md`.”

- [ ] **Step 2: Run the active-document scan**

```powershell
rg -n -i "Inno Setup|install-build-tools|PublicCloudDownloader\.iss|InstallerPath|Setup\.exe|per-user Windows installer|ZIP and installer" scripts README.md docs/PublicCloudDownloader-README.txt docs/requirements
```

Expected: no matches. Do not scan or rewrite historical `docs/superpowers/plans` or older specs.

- [ ] **Step 3: Re-run the source contract**

```powershell
./scripts/zip-only-release-test.ps1
```

Expected: pass.

- [ ] **Step 4: Commit tracked documentation without absorbing unrelated untracked files**

```powershell
git add -- README.md docs/PublicCloudDownloader-README.txt docs/requirements/2026-08-14-versioning-and-release-requirements.md
git diff --cached --check
git commit -m "docs: document portable zip releases"
```

The Octicons requirement file is currently an untracked pre-existing user change. Modify its one acceptance line but leave the file untracked for the existing Octicons delivery unless the coordinator explicitly authorizes staging that complete file.

---

### Task 4: Verify runtime preservation and generate the final ZIP

**Files:**

- Verify: all source files and scripts
- Generate: `dist/PublicCloudDownloader/**`
- Generate: `artifacts/PublicCloudDownloader-v1.1.2-win-x64.zip`
- Generate: `artifacts/SHA256SUMS.txt`
- Generate: `artifacts/verification.txt`

**Interfaces:**

- Consumes: completed ZIP-only scripts and the existing 1.1.2 UI/Octicons worktree.
- Produces: verified release artifacts without external publication.

- [ ] **Step 1: Run source verification**

```powershell
./scripts/zip-only-release-test.ps1
./scripts/version-test.ps1 -ExpectedVersion 1.1.2
dotnet build PublicCloudDownloader.sln -c Release
dotnet test PublicCloudDownloader.sln -c Release --no-build
```

Expected: both script checks pass, build has zero warnings/errors, and all test cases pass.

- [ ] **Step 2: Run the normal build output self-test**

```powershell
$selfTest = Start-Process `
    -FilePath ./src/PublicCloudDownloader.App/bin/Release/net8.0-windows/PublicCloudDownloader.exe `
    -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
if ($selfTest.ExitCode -ne 0) { throw "Built self-test exited $($selfTest.ExitCode)." }
```

Expected: exit `0`.

- [ ] **Step 3: Seed preservation sentinels and record their hashes**

Create only task-owned test files:

```powershell
$publishRoot = [System.IO.Path]::GetFullPath('./dist/PublicCloudDownloader')
$dataSentinel = Join-Path $publishRoot 'data\zip-only-preserve-test.txt'
$logSentinel = Join-Path $publishRoot 'logs\zip-only-preserve-test.txt'
New-Item -ItemType Directory -Path (Split-Path $dataSentinel), (Split-Path $logSentinel) -Force | Out-Null
Set-Content -LiteralPath $dataSentinel -Value 'preserve-data-1.1.2' -Encoding ascii
Set-Content -LiteralPath $logSentinel -Value 'preserve-logs-1.1.2' -Encoding ascii
$beforeHashes = @{
    Data = (Get-FileHash -LiteralPath $dataSentinel -Algorithm SHA256).Hash
    Logs = (Get-FileHash -LiteralPath $logSentinel -Algorithm SHA256).Hash
}
```

- [ ] **Step 4: Run full packaging**

```powershell
./scripts/package.ps1
```

Expected: no Inno Setup output; publish self-test, publish validation, ZIP validation, and extracted self-test pass; artifacts contain one ZIP and two metadata files.

- [ ] **Step 5: Prove sentinel preservation**

```powershell
$afterHashes = @{
    Data = (Get-FileHash -LiteralPath $dataSentinel -Algorithm SHA256).Hash
    Logs = (Get-FileHash -LiteralPath $logSentinel -Algorithm SHA256).Hash
}
if ($beforeHashes.Data -cne $afterHashes.Data -or $beforeHashes.Logs -cne $afterHashes.Logs) {
    throw 'Packaging changed preserved runtime sentinel content.'
}
```

- [ ] **Step 6: Validate generated artifacts and exact ZIP entries**

```powershell
./scripts/zip-only-release-test.ps1 -ArtifactsDirectory ./artifacts

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Resolve-Path ./artifacts/PublicCloudDownloader-v1.1.2-win-x64.zip
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $actual = @($zip.Entries.FullName | Sort-Object)
    $expected = @(
        'PublicCloudDownloader.exe',
        'PublicCloudDownloader.ico',
        'README.txt',
        'THIRD-PARTY-NOTICES.md',
        'data/',
        'logs/'
    ) | Sort-Object
    if (Compare-Object $expected $actual) { throw 'ZIP entry set differs from the six-entry allowlist.' }
} finally {
    $zip.Dispose()
}
```

Expected: contract passes; exact six-entry ZIP; no preserved sentinel occurs in the ZIP.

- [ ] **Step 7: Remove only task-owned sentinel files**

Resolve both paths, require each parent to equal the expected publish `data` or `logs` directory, then remove the two literal files. Do not remove either directory or any other runtime content.

```powershell
foreach ($sentinel in @($dataSentinel, $logSentinel)) {
    $resolved = [System.IO.Path]::GetFullPath($sentinel)
    $parent = [System.IO.Path]::GetFullPath((Split-Path $resolved))
    $allowedParents = @(
        [System.IO.Path]::GetFullPath((Join-Path $publishRoot 'data')),
        [System.IO.Path]::GetFullPath((Join-Path $publishRoot 'logs'))
    )
    if ($parent -notin $allowedParents -or [System.IO.Path]::GetFileName($resolved) -ne 'zip-only-preserve-test.txt') {
        throw "Refused unsafe sentinel cleanup: $resolved"
    }
    Remove-Item -LiteralPath $resolved -Force
}
```

- [ ] **Step 8: Confirm Inno Setup was not changed**

```powershell
$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
$item = Get-Item -LiteralPath $iscc
$hash = (Get-FileHash -LiteralPath $iscc -Algorithm SHA256).Hash.ToLowerInvariant()
if ($item.Length -ne 1456272 -or $hash -cne '0a8757031b33777e4c9cbffee40f11a5062b36d25cbe144c1db73b6102b80ad7') {
    throw 'Installed Inno Setup baseline changed.'
}
```

- [ ] **Step 9: Review final source state**

```powershell
git diff --check
git status --short
git log -5 --oneline
Get-Content ./artifacts/verification.txt
Get-FileHash ./artifacts/PublicCloudDownloader-v1.1.2-win-x64.zip -Algorithm SHA256
```

Expected: no whitespace errors; only known pre-existing 1.1.2 UI/Octicons work remains uncommitted; verification evidence lists every PASS stage; report the final ZIP path and SHA-256.

## Acceptance Checklist

- [ ] Active source and documentation contain no installer or Inno Setup path.
- [ ] The `.iss` and installer build-tool script are deleted from Git.
- [ ] Local `dist/data` and `dist/logs` files survive packaging unchanged.
- [ ] ZIP `data/` and `logs/` are empty.
- [ ] Published and extracted-ZIP self-tests exit `0`.
- [ ] ZIP has exactly the approved six root entries.
- [ ] Artifacts contain exactly one versioned ZIP plus two metadata files.
- [ ] `SHA256SUMS.txt` contains exactly one correct lowercase ZIP hash.
- [ ] `verification.txt` records every required stage.
- [ ] Version check, zero-warning build, and full automated test suite pass.
- [ ] Installed Inno Setup remains present and unchanged.
- [ ] No unrelated XAML/icon behavior or main-worktree file is modified.
