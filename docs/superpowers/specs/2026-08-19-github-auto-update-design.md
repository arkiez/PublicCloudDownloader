# GitHub Auto-Update v1.2.0 Design

## Goal

Add a self-update system to Public Cloud Downloader that checks GitHub Releases without a private server, offers an explicit **Update now** / **Later** choice, verifies the downloaded package before installation, preserves user runtime data, restarts the app after a successful update, and makes the repository public only after a repository/history security scan.

## Version and Repository

- First updater-enabled release: `v1.2.0`.
- Repository: `arkiez/PublicCloudDownloader`.
- Repository remains private during development and security review.
- Repository changes to public only after the release/update implementation passes tests and secret/history scans.
- No GitHub token, API key, cookie, or credential is embedded in the application.

## Update Source

The application reads the latest published release from:

`GET https://api.github.com/repos/arkiez/PublicCloudDownloader/releases/latest`

The update client accepts only a non-draft, non-prerelease release with a semantic tag in the form `vMAJOR.MINOR.PATCH`. The application compares that release version to its current assembly version.

For public repositories, release metadata and public release assets are fetched without authentication. The updater uses `browser_download_url` for the ZIP package and requires a SHA-256 digest from the release asset metadata before installation.
## User Experience

Update checks run asynchronously after the main window is loaded. A failed update check must never prevent the normal downloader workflow from opening or functioning.

When a newer stable release is available, the app shows a compact update prompt containing the current version, latest version, release notes summary, and two actions:

- `Update now`
- `Later`

The main window also provides a manual `Check for updates` action so the user can request the same check on demand.

While an update is downloading, show the package download progress. If verification or installation fails, show a safe error and leave the currently installed application unchanged.

## Package Contract

Each GitHub Release must contain these assets:

- `PublicCloudDownloader-v<version>-win-x64.zip`
- `SHA256SUMS.txt`
- `verification.txt`

The ZIP payload remains portable and contains `PublicCloudDownloader.exe`, `PublicCloudDownloader.ico`, `README.txt`, `THIRD-PARTY-NOTICES.md`, plus empty `data/` and `logs/` directories. Runtime `data` and `logs` already beside the installed executable are never replaced or deleted during update.
## Download and Verification

The updater downloads the ZIP into an application-owned temporary directory under `%TEMP%`. The package is not trusted merely because it came from a GitHub URL.

Before extraction, the updater computes SHA-256 over the downloaded ZIP and compares it to the release asset `digest`. The digest must use the `sha256:<hex>` form. A missing, malformed, or mismatched digest causes the update to stop before extraction.

The updater extracts only expected package entries and rejects unsafe ZIP paths that would escape the staging directory. The staged executable must exist and report the same version as the selected GitHub release before installation continues.

## Self-Update Process

The normal application launches the staged new `PublicCloudDownloader.exe` with an internal `--apply-update` mode. Arguments identify the current installation directory, the old process ID, and the executable path to restart.

The new executable in update mode:

1. Waits for the old process to exit.
2. Copies only generated application payload files from staging into the installation directory.
3. Leaves existing `data/` and `logs/` directories untouched.
4. Starts the replaced `PublicCloudDownloader.exe` normally.
5. Deletes temporary staging files when practical; cleanup failure is non-fatal.

The update operation must fail safely if the installation directory is not writable. It must not partially delete the existing application before validated replacement files are available.
## Error Handling and Privacy

Network failures, GitHub rate limits, malformed release metadata, missing assets, checksum failures, invalid ZIP contents, and update-copy failures are handled as update-only failures. They do not break public-cloud downloading.

Update logs must not contain credentials, authorization headers, signed URLs, cookies, or other secrets. The application does not request GitHub authentication from end users.

## Release Workflow

A release is created only from a clean, tested `main` commit. The release process performs version checks, full tests, publish/self-test validation, ZIP validation, and SHA-256 generation before a Git tag or published release is treated as current.

The release workflow creates tag `v<version>` and a GitHub Release whose ZIP asset name exactly matches the package contract. `SHA256SUMS.txt` and `verification.txt` are uploaded alongside it. Release publishing must not occur when package validation fails.

## Public Repository Safety Gate

Before changing repository visibility from private to public, scan both the current tree and reachable Git history for likely secrets and sensitive files. At minimum inspect for private keys, access tokens, passwords, API keys, `.env` files, credential/config exports, and accidental runtime data/log files.

If any credible secret or sensitive artifact is found, stop before changing visibility and report the finding for remediation. Simply deleting a secret from the current tree is insufficient if it remains in reachable Git history.

After the scan is clean, change `arkiez/PublicCloudDownloader` from PRIVATE to PUBLIC and verify repository visibility from GitHub after the change.
## Testing Requirements

Automated tests must cover:

- Newer semantic version is detected as an update.
- Equal or older versions do not produce an update prompt.
- Draft and prerelease versions are rejected.
- Update-check network/API failure leaves the main application usable.
- Missing or malformed release assets are rejected.
- SHA-256 mismatch prevents extraction and installation.
- Unsafe ZIP paths are rejected.
- The staged executable version must match the target release.
- Update mode waits for the previous process before file replacement.
- Application payload files are replaced while existing `data/` and `logs/` are preserved.
- Restart is requested only after successful replacement.
- Existing downloader and release tests remain green.

A release candidate is complete only when the full test suite, release package validation, published-payload self-test, and update-specific tests all pass from the v1.2.0 source tree.

## Non-Goals

- No background service or Windows service.
- No installer/MSI/MSIX migration in v1.2.0.
- No silent update without user approval.
- No beta/prerelease update channel.
- No private GitHub API credentials bundled with the application.
