# GitHub Auto-Update v1.2.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Public Cloud Downloader v1.2.0 with opt-in self-update from public GitHub Releases, package verification, safe in-place replacement, manual/automatic update UI, and a guarded PRIVATE→PUBLIC repository transition.

**Architecture:** Add a focused update subsystem under `PublicCloudDownloader.App/Updates`. `GitHubUpdateClient` discovers stable releases, `UpdatePackageService` downloads/verifies/stages ZIPs, and `SelfUpdateRunner` applies staged payloads from a new-process `--apply-update` mode. WPF only coordinates UI; update logic remains independently testable.

**Tech Stack:** .NET 8, WPF, `HttpClient`, `System.Text.Json`, `System.IO.Compression`, `System.Security.Cryptography`, xUnit, PowerShell, GitHub CLI.

**Spec:** `docs/superpowers/specs/2026-08-19-github-auto-update-design.md`

## Global Constraints

- First updater-enabled release is exactly `v1.2.0`.
- Repository is `arkiez/PublicCloudDownloader`; it stays private until the security gate passes.
- No GitHub token/API key/cookie/credential is embedded in the app.
- Only non-draft, non-prerelease `vMAJOR.MINOR.PATCH` releases are eligible.
- ZIP asset name is `PublicCloudDownloader-v<version>-win-x64.zip`.
- A valid `sha256:<hex>` release-asset digest is mandatory before extraction.
- Existing `data/` and `logs/` beside the executable are never replaced or deleted.
- Update failure must not prevent normal public-cloud downloads.
- The footer `Update v<version>` button is visible only for an available newer release and is otherwise `Collapsed`.
- Successful downloads show both the existing in-app completion status and a non-blocking Windows notification.
- No silent update; the user must choose `Update now`.
- No installer/MSI/MSIX or background service in v1.2.0.

---

### Task 1: Version and release-model foundation

**Files:**
- Modify: `Version.props`
- Create: `src/PublicCloudDownloader.App/Updates/UpdateRelease.cs`
- Create: `src/PublicCloudDownloader.App/Updates/UpdateVersion.cs`
- Create: `tests/PublicCloudDownloader.Tests/Updates/UpdateVersionTests.cs`

**Interfaces:**
- Produces: `UpdateRelease`, `UpdateAsset`, and `UpdateVersion.TryParseTag(string, out Version)`.
- Later tasks consume these types without depending on WPF.

- [ ] **Step 1: Write failing version-selection tests**

```csharp
[Theory]
[InlineData("v1.2.1", "1.2.0", true)]
[InlineData("v1.2.0", "1.2.0", false)]
[InlineData("v1.1.9", "1.2.0", false)]
public void IsNewer_compares_stable_versions(string tag, string current, bool expected)
{
    Assert.Equal(expected, UpdateVersion.IsNewer(tag, Version.Parse(current)));
}

[Theory]
[InlineData("1.2.1")]
[InlineData("v1.2")]
[InlineData("v1.2.1-beta.1")]
public void IsNewer_rejects_non_release_tags(string tag) => Assert.False(UpdateVersion.IsNewer(tag, new Version(1,2,0)));
```

- [ ] **Step 2: Run RED**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter UpdateVersionTests`
Expected: FAIL because update types do not exist.- [ ] **Step 3: Implement minimal version/model code and bump version**

`Version.props` becomes `1.2.0 / 1.2.0.0`. Add:

```csharp
public sealed record UpdateAsset(string Name, Uri DownloadUri, string Digest, long Size);
public sealed record UpdateRelease(Version Version, string Tag, string Notes, UpdateAsset Package);

public static class UpdateVersion
{
    private static readonly Regex StableTag = new("^v(?<v>\\d+\\.\\d+\\.\\d+)$", RegexOptions.CultureInvariant);
    public static bool IsNewer(string tag, Version current)
        => TryParseTag(tag, out var candidate) && candidate > current;
    public static bool TryParseTag(string tag, out Version version)
    {
        var match = StableTag.Match(tag ?? string.Empty);
        return Version.TryParse(match.Success ? match.Groups["v"].Value : string.Empty, out version!);
    }
}
```

- [ ] **Step 4: Run GREEN plus existing version test**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter UpdateVersionTests; powershell -ExecutionPolicy Bypass -File scripts/version-test.ps1 -ExpectedVersion 1.2.0`
Expected: PASS.

- [ ] **Step 5: Commit**
`git add Version.props src/PublicCloudDownloader.App/Updates tests/PublicCloudDownloader.Tests/Updates/UpdateVersionTests.cs && git commit -m "feat: add update version model"`

### Task 2: GitHub latest-release discovery client

**Files:**
- Create: `src/PublicCloudDownloader.App/Updates/IUpdateClient.cs`
- Create: `src/PublicCloudDownloader.App/Updates/GitHubUpdateClient.cs`
- Create: `tests/PublicCloudDownloader.Tests/Updates/GitHubUpdateClientTests.cs`

**Interfaces:**
- Produces: `Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken)`.
- `null` means no newer eligible stable release; malformed metadata throws `UpdateCheckException`.

- [ ] **Step 1: Write failing GitHub response tests**

Create tests using a custom `HttpMessageHandler` that returns JSON. Cover: newer stable release returns `UpdateRelease`; equal version returns null; `draft=true` returns null; `prerelease=true` returns null; missing ZIP asset throws; missing/malformed `digest` throws; non-2xx response throws `UpdateCheckException`.

Representative fixture:

```csharp
const string Json = """
{"tag_name":"v1.2.1","draft":false,"prerelease":false,"body":"Bug fixes","assets":[{"name":"PublicCloudDownloader-v1.2.1-win-x64.zip","browser_download_url":"https://example.test/app.zip","digest":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa","size":123}]}
""";
```

- [ ] **Step 2: Run RED**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter GitHubUpdateClientTests`
Expected: FAIL because client/interface do not exist.

- [ ] **Step 3: Implement the client**
Use one injected `HttpClient`, set `User-Agent: PublicCloudDownloader/<assembly-version>`, GET exactly `https://api.github.com/repos/arkiez/PublicCloudDownloader/releases/latest`, deserialize only required fields, reject draft/prerelease/invalid tags, and require exact ZIP asset naming plus lowercase/uppercase-insensitive 64-hex SHA-256 digest.

```csharp
public interface IUpdateClient
{
    Task<UpdateRelease?> CheckAsync(Version currentVersion, CancellationToken cancellationToken);
}

public sealed class UpdateCheckException(string message, Exception? inner = null) : Exception(message, inner);
```

- [ ] **Step 4: Run GREEN**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter GitHubUpdateClientTests`
Expected: PASS.

- [ ] **Step 5: Commit**
`git add src/PublicCloudDownloader.App/Updates tests/PublicCloudDownloader.Tests/Updates/GitHubUpdateClientTests.cs && git commit -m "feat: discover updates from GitHub Releases"`

### Task 3: Verified package download and safe staging

**Files:**
- Create: `src/PublicCloudDownloader.App/Updates/UpdatePackageService.cs`
- Create: `src/PublicCloudDownloader.App/Updates/UpdatePackageException.cs`
- Create: `tests/PublicCloudDownloader.Tests/Updates/UpdatePackageServiceTests.cs`

**Interfaces:**
- Produces: `Task<StagedUpdate> DownloadAndStageAsync(UpdateRelease release, IProgress<double>? progress, CancellationToken cancellationToken)`.
- `StagedUpdate` contains `RootPath`, `ExecutablePath`, `TargetVersion`; caller owns cleanup after handoff.

- [ ] **Step 1: Write failing checksum, ZIP-safety, and progress tests**

Tests create ZIP bytes in memory. Cover: correct SHA stages expected payload; wrong SHA throws before extraction; `../escape.txt` and rooted ZIP paths throw; missing `PublicCloudDownloader.exe` throws; unexpected top-level files throw; existing temp destination is not reused; progress reaches 100 for known content length.

Expected payload whitelist:

```csharp
private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
{
    "PublicCloudDownloader.exe", "PublicCloudDownloader.ico", "README.txt", "THIRD-PARTY-NOTICES.md"
};
```

- [ ] **Step 2: Run RED**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter UpdatePackageServiceTests`
Expected: FAIL because package service does not exist.

- [ ] **Step 3: Implement minimal secure staging**
Download to `%TEMP%\PublicCloudDownloader\updates\<guid>\package.zip`, stream through `IncrementalHash.CreateHash(HashAlgorithmName.SHA256)`, compare the final hex to `release.Package.Digest[7..]`, then extract only allowed files and directory entries `data/` and `logs/`. Resolve every entry with `Path.GetFullPath` and require it to stay under the staging root.

- [ ] **Step 4: Validate staged executable version**
Use `FileVersionInfo.GetVersionInfo(exe).FileVersion`, parse `MAJOR.MINOR.PATCH.0`, and require it to equal `release.Version`; otherwise throw `UpdatePackageException` and delete staging.

- [ ] **Step 5: Run GREEN and full existing tests**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release`
Expected: all tests PASS.

- [ ] **Step 6: Commit**
`git add src/PublicCloudDownloader.App/Updates tests/PublicCloudDownloader.Tests/Updates/UpdatePackageServiceTests.cs && git commit -m "feat: verify and stage update packages"`

### Task 4: Self-update apply mode and restart

**Files:**
- Create: `src/PublicCloudDownloader.App/Updates/SelfUpdateRunner.cs`
- Modify: `src/PublicCloudDownloader.App/App.xaml.cs`
- Create: `tests/PublicCloudDownloader.Tests/Updates/SelfUpdateRunnerTests.cs`

**Interfaces:**
- Produces: `Task<int> ApplyAsync(string stagingRoot, string installDirectory, int oldProcessId, string restartExecutable, CancellationToken cancellationToken)`.
- `App.OnStartup` recognizes `--apply-update <stagingRoot> <installDirectory> <oldPid> <restartExecutable>` before creating WPF UI.

- [ ] **Step 1: Write failing apply-mode tests**
Use temporary install/staging directories and an injectable `IProcessController`. Verify the runner waits for the supplied old PID, replaces only the four generated payload files, keeps pre-existing `data/user.db` and `logs/history.log` byte-for-byte, starts the install-directory EXE only after copy success, and returns nonzero on unwritable/copy failure without deleting runtime data.

```csharp
public interface IProcessController
{
    Task WaitForExitAsync(int processId, CancellationToken cancellationToken);
    void Start(string executablePath, string? arguments = null);
}
```

- [ ] **Step 2: Run RED**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter SelfUpdateRunnerTests`
Expected: FAIL because runner/process interface do not exist.

- [ ] **Step 3: Implement transactional-enough payload replacement**
Before overwriting, require all four staged files to exist. Copy each staged file to `<install>\.<name>.update-new`, then replace/move temp files into final names. Never enumerate/delete the install directory; never copy staged `data` or `logs`. On failure, delete only `.update-new` files and return `1`.

- [ ] **Step 4: Wire `--apply-update` startup**
Parse exact argument count/types in `App.xaml.cs`; run apply logic before `base.OnStartup` opens normal UI. Invalid arguments exit code `2`. Normal startup, `--self-test`, and `--headless-download` behavior remain unchanged.

- [ ] **Step 5: Run GREEN**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter "SelfUpdateRunnerTests|AppSelfTestTests"`
Expected: PASS.

- [ ] **Step 6: Commit**
`git add src/PublicCloudDownloader.App/App.xaml.cs src/PublicCloudDownloader.App/Updates tests/PublicCloudDownloader.Tests/Updates/SelfUpdateRunnerTests.cs && git commit -m "feat: apply staged updates safely"`

### Task 5: Update UI, automatic check, and manual check

**Files:**
- Create: `src/PublicCloudDownloader.App/UpdatePromptWindow.xaml`
- Create: `src/PublicCloudDownloader.App/UpdatePromptWindow.xaml.cs`
- Create: `src/PublicCloudDownloader.App/Notifications/IDesktopNotifier.cs`
- Create: `src/PublicCloudDownloader.App/Notifications/WindowsDesktopNotifier.cs`
- Modify: `src/PublicCloudDownloader.App/MainWindow.xaml`
- Modify: `src/PublicCloudDownloader.App/MainWindow.xaml.cs`
- Create: `tests/PublicCloudDownloader.Tests/Updates/UpdateUiCoordinatorTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Notifications/DownloadCompletionNotificationTests.cs`
- Create: `src/PublicCloudDownloader.App/Updates/UpdateUiCoordinator.cs`

**Interfaces:**
- `UpdateUiCoordinator.CheckAsync(bool userInitiated, CancellationToken)` returns an `UpdateCheckResult` describing `NoUpdate`, `Available`, or `Failed` without showing WPF itself.
- MainWindow owns dialogs and calls coordinator once after `Loaded`; manual footer button calls the same coordinator with `userInitiated=true`.

- [ ] **Step 1: Write failing coordinator behavior tests**
Cover: automatic network failure returns `Failed` but does not throw; manual no-update returns `NoUpdate`; available release returns version/notes; only one check runs at a time; current version comes from the executing assembly.

- [ ] **Step 2: Run RED**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter UpdateUiCoordinatorTests`
Expected: FAIL.

- [ ] **Step 3: Implement coordinator and compact prompt**
The prompt shows `Update available`, current/latest versions, a trimmed release-notes body, `Update now`, and `Later`. `Update now` downloads/stages with progress text/bar, launches the staged EXE with `--apply-update`, then closes the current app. `Later` closes only the prompt.

- [ ] **Step 4: Add manual footer action without enlarging the fixed window**
Replace the footer's right-side single version TextBlock with a horizontal StackPanel containing a text-style `Check for updates` Button, a compact `Update v<version>` button, and `VersionText`. Bind the update button visibility to the available-release state using `Collapsed` when unavailable so it consumes no space. Keep `Width=900`, `Height=470`, `ResizeMode=CanMinimize`; no scrollbar is introduced.

- [ ] **Step 5: Implement startup notification policy**
Attach `Loaded += async ...` once. Automatic check is silent for `NoUpdate` and `Failed`; it opens the prompt only for `Available`. Manual check explicitly shows `You're up to date` or `Could not check for updates` when appropriate.

- [ ] **Step 6: Add download-complete Windows notification**
Add `IDesktopNotifier.ShowDownloadComplete(string summary, string destinationPath)` and a portable Win32/WinForms-backed implementation that shows a non-blocking notification without turning the app into a persistent tray application. Trigger it only when `DownloadMonitorWindow` reports a completed download; keep the existing in-app `LinkStatus` completion text. Add tests around the completion-notification decision/summary so cancelled or failed downloads do not notify.

- [ ] **Step 7: Run targeted and full tests**
Run: `dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release`
Expected: all tests PASS and existing downloader tests unchanged.

- [ ] **Step 8: Commit**
`git add src/PublicCloudDownloader.App tests/PublicCloudDownloader.Tests/Updates/UpdateUiCoordinatorTests.cs && git commit -m "feat: add update checks and notification UI"`

### Task 6: Release publishing and public-repository safety gate

**Files:**
- Create: `scripts/security-scan-public.ps1`
- Create: `scripts/publish-github-release.ps1`
- Modify: `scripts/package.ps1`
- Modify: `scripts/release-test.ps1`
- Modify: `docs/PublicCloudDownloader-README.txt`
- Create: `tests/PublicCloudDownloader.Tests/Release/UpdateReleaseContractTests.cs`

**Interfaces:**
- `security-scan-public.ps1` exits `0` only when current tree and reachable history pass configured secret/sensitive-file checks.
- `publish-github-release.ps1 -Version 1.2.0` refuses dirty/non-main/private-or-unscanned state and uploads exactly three release assets.

- [ ] **Step 1: Write failing release-contract test**
Assert package ZIP contains exactly the four payload files plus `data/` and `logs/`, version is `1.2.0.0`, and the artifact directory contains exactly ZIP + `SHA256SUMS.txt` + `verification.txt` after packaging.

- [ ] **Step 2: Add deterministic local public-safety scan**
Scan tracked current files and `git log -p --all --no-ext-diff` for PEM private-key markers, GitHub token prefixes (`ghp_`, `github_pat_`), common `api[_-]?key|password|secret|token` assignments with non-placeholder values, `.env`/credential exports, and tracked `data/`/`logs/` runtime contents. Print file/commit context only; never echo the full suspected secret value.

- [ ] **Step 3: Add GitHub release publisher**
Require `gh auth status`, clean `main`, `origin/main == HEAD`, version-test PASS, full tests PASS, package script PASS, security scan PASS, and repository visibility `PUBLIC`. Then create annotated tag `v1.2.0`, push it, and run:

```powershell
gh release create "v$Version" `
  "artifacts/PublicCloudDownloader-v$Version-win-x64.zip" `
  "artifacts/SHA256SUMS.txt" `
  "artifacts/verification.txt" `
  --repo arkiez/PublicCloudDownloader `
  --title "Public Cloud Downloader v$Version" `
  --generate-notes `
  --latest
```

- [ ] **Step 4: Update README copy**
Document automatic startup checks, `Check for updates`, `Update now`/`Later`, GitHub Releases as update source, no bundled credentials, SHA-256 verification, and preservation of `data/logs`.

- [ ] **Step 5: Run release tests**
Run: `powershell -ExecutionPolicy Bypass -File scripts/package.ps1; dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release`
Expected: package verification PASS and all tests PASS.

- [ ] **Step 6: Commit**
`git add scripts docs tests/PublicCloudDownloader.Tests/Release && git commit -m "build: add guarded GitHub release workflow"`

### Task 7: Final verification, PUBLIC transition, and v1.2.0 release

**Files:**
- No production-code changes expected after verification unless a test/security finding requires a fix.
- GitHub repository settings and Release metadata are changed only after all local gates pass.

- [ ] **Step 1: Run fresh complete verification from the feature branch**
Run:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/version-test.ps1 -ExpectedVersion 1.2.0
dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File scripts/package.ps1
powershell -ExecutionPolicy Bypass -File scripts/security-scan-public.ps1
```

Expected: version PASS; zero failed tests; package/self-test/ZIP validation PASS; public-safety scan PASS.

- [ ] **Step 2: Merge the tested feature branch to `main` and re-run gates**
Merge only after the feature branch is clean. On merged `main`, repeat version test, full test suite, package validation, and security scan. Stop on any difference or failure.

- [ ] **Step 3: Push tested `main` and verify remote hash**
Run `git push origin main`, then compare `git rev-parse HEAD` with `git ls-remote origin refs/heads/main`. They must match exactly.

- [ ] **Step 4: Change repository visibility only after the scan is green**
Run:

```powershell
gh repo edit arkiez/PublicCloudDownloader --visibility public --accept-visibility-change-consequences
gh repo view arkiez/PublicCloudDownloader --json visibility,isPrivate
```

Expected: `visibility` is `PUBLIC` and `isPrivate` is `false`. If the scan found any credible secret, do not run this step.

- [ ] **Step 5: Publish v1.2.0 release**
Run: `powershell -ExecutionPolicy Bypass -File scripts/publish-github-release.ps1 -Version 1.2.0`
Expected: published latest release `v1.2.0` with exactly ZIP, `SHA256SUMS.txt`, and `verification.txt` assets.

- [ ] **Step 6: Verify anonymous update metadata and asset contract**
Without sending Authorization headers, request the public latest-release endpoint and confirm tag `v1.2.0`, `draft=false`, `prerelease=false`, exact ZIP asset name, valid `sha256:` digest, and successful public asset download.

- [ ] **Step 7: Final smoke check**
Launch the packaged v1.2.0 EXE, use manual `Check for updates`, verify it reports up-to-date against v1.2.0, then run `--self-test` once more. Preserve the generated ZIP and checksum as the current release artifacts.

---

## Self-Review Results

- Spec coverage: all requirements map to Tasks 1–7, including auto/manual UI, safe replacement, `data/logs` preservation, release publishing, public transition, and anonymous post-release verification.
- Placeholder scan: no `TBD`, `TODO`, `implement later`, or undefined follow-up steps.
- Type consistency: Tasks 1–5 share `UpdateRelease`, `UpdateAsset`, `IUpdateClient`, `StagedUpdate`, and `IProcessController` consistently.
- Scope: updater implementation and release/publication are sequential parts of one v1.2.0 delivery; neither is independently useful for the requested public GitHub update channel.
