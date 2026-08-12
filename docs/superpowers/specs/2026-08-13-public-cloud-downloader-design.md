# Public Cloud Downloader Design

**Date:** 2026-08-13  
**Status:** Approved  
**Initial version:** 1.0.0

## 1. Purpose

Public Cloud Downloader is a new Windows desktop application for downloading
public Google Drive and OneDrive Personal files and folders to a local folder
without a user account, OAuth flow, or provider sign-in. It replaces none of
the source code in the existing Rclone Transfer Manager repository; it lives in
its own repository at `C:\Users\Acer27arkiez\Document\PublicCloudDownloader`.

The application has one directional workflow:

`Public cloud file or folder link -> Local Windows folder`

It is not based on rclone and does not contain sync, upload, cloud destination,
account management, or authenticated-provider functionality.

## 2. Supported Scope

Version 1.0.0 supports:

- Public Google Drive file links.
- Public Google Drive folder links, including nested folders.
- Public OneDrive Personal file links.
- Public OneDrive Personal folder links, including nested folders.
- Recursive manifest enumeration before downloading.
- Local destinations selected by the user.
- Existing-file detection and one confirmation decision per download job.
- Progress, cancellation, retry, safe partial files, and completion reporting.
- Google Docs export to `.docx`, Google Sheets export to `.xlsx`, and Google
  Slides export to `.pptx` when those public items permit export.
- Public shortcuts whose targets remain within the supported providers and are
  anonymously accessible.

Version 1.0.0 explicitly excludes:

- OneDrive for Business and SharePoint links.
- Private, organization-only, expired, or sign-in-required links.
- Provider accounts, OAuth, API tokens, saved cookies, and credentials.
- Uploads and cloud destinations.
- Sync, bidirectional sync, mirroring, and deletion of destination-only files.
- Local sources.
- Background services, scheduled tasks, shell extensions, and automatic
  updates.
- Providers other than Google Drive and OneDrive Personal.

## 3. Product Identity

- Product name: `Public Cloud Downloader`.
- Executable name: `PublicCloudDownloader.exe`.
- Initial semantic version: `1.0.0`.
- Windows platform: Windows 10 and Windows 11, x64.
- Runtime: .NET 8 WPF, self-contained; no separately installed .NET runtime is
  required.
- All visible versions, assembly versions, package names, and installer
  versions derive from one canonical repository version property.

## 4. User Experience

### 4.1 Main window

The UI is English-only and follows the approved **Layout A, Revision 2**
focused-form design. The main window contains:

1. A compact header with the product icon, `Public Cloud Downloader`, the
   subtitle `Download public cloud folders to your PC`, and an `About` button.
2. A card headed `Download a public folder` with explanatory text stating that
   Google Drive and OneDrive are supported without sign-in.
3. A full-width `PUBLIC FOLDER LINK` input and a `Paste` action.
4. Inline format status that names the recognized provider. It must say that
   the *link format* is recognized; it must not claim that the link is public
   before the anonymous preflight completes.
5. A `SAVE TO` local folder input and `Browse` button.
6. A summary explaining where the source root folder will be created.
7. A compact, right-aligned `Download` button.
8. A footer stating `Copy only - Existing files are never changed without
   confirmation` and displaying the canonical version.

The `Download` button is disabled unless both conditions are true:

- The source contains a complete, syntactically supported Google Drive or
  OneDrive Personal file/folder link.
- The destination is an existing, writable local directory.

Syntactic validation only enables the button. Clicking `Download` performs the
network-based anonymous-access validation.

### 4.2 Destination layout

For a folder source named `Project Assets` and destination base
`D:\Downloads`, the program creates and populates:

`D:\Downloads\Project Assets\...`

The complete source hierarchy is retained below that folder. For a single-file
source, the file is written directly inside the selected destination base, for
example `D:\Downloads\report.pdf`.

Provider names are sanitized for Windows. Reserved device names, trailing dots
or spaces, invalid characters, absolute paths, and parent traversal segments
must never escape or corrupt the selected destination. If sanitization maps two
source items to the same case-insensitive Windows path, the manifest stage
assigns deterministic numeric suffixes and shows the final names in the job
summary and log.

### 4.3 Preflight and manifest

After the user clicks `Download`, the main window enters a checking state and
prevents a second job from starting. The selected anonymous provider adapter:

1. Resolves provider redirects and short links.
2. Verifies that the item is accessible without provider credentials.
3. Verifies that downloads are permitted.
4. Determines whether the item is a file or folder.
5. Enumerates the complete recursive manifest, following pagination.
6. Resolves supported public shortcuts while detecting cycles.
7. Produces source names, relative paths, sizes when available, item types, and
   provider download descriptors.

No destination root folder or file is created until this preflight completes
successfully. If the link is private, expired, requires sign-in, disables
download, is an unsupported business/SharePoint link, or cannot be enumerated,
the app shows a warning popup and returns to the editable main form.

The warning title is `This folder is not public` for access failures. Its body
explains that the link may require sign-in, have expired, or disallow downloads,
and suggests enabling `Anyone with the link`. Unsupported provider variants
use a specific message rather than incorrectly reporting a private link.

### 4.4 Existing-file confirmation

The application compares the finalized manifest with the local destination by
case-insensitive Windows path before downloading. When no destination files
conflict, the job proceeds immediately. When one or more files already exist,
a modal confirmation window shows the conflict count and paths and offers:

- `Cancel`: no downloads start and no existing file changes.
- `Skip existing`: every existing destination file is retained and skipped.
- `Overwrite existing`: every conflict is downloaded to a temporary file and
  replaces the old file only after the new content completes successfully.

The decision applies to all conflicts in that job. There are no per-file
popups.

### 4.5 Download monitor

The monitor displays overall progress, current file, completed file count,
transferred bytes when sizes are known, failures, elapsed time, and a `Cancel`
button. It must remain useful when a provider does not expose every file size;
in that case file-count progress remains authoritative and byte progress is
shown only for known content lengths.

Cancellation stops new downloads, cancels active requests, and removes
`.partial` files created by that job. Completed files remain. The app does not
delete destination-only content.

Completion states are:

- `Completed`: every planned item succeeded or was intentionally skipped.
- `Completed with errors`: at least one item failed after retries; successful
  files remain and failed relative paths are shown.
- `Cancelled`: the user cancelled the job.
- `Failed`: preflight or a job-level condition prevented useful completion.

## 5. Architecture

The repository is a .NET solution split by responsibility:

- `PublicCloudDownloader.App`: WPF windows, dialogs, view models, application
  composition, and accessibility metadata.
- `PublicCloudDownloader.Core`: provider-neutral models, link selection,
  manifest normalization, collision analysis, policies, and download
  coordination interfaces.
- `PublicCloudDownloader.Providers.GoogleDrive`: anonymous Google Drive public
  share resolution, enumeration, export descriptors, and download streams.
- `PublicCloudDownloader.Providers.OneDrivePersonal`: anonymous OneDrive
  Personal public share resolution, enumeration, and download streams.
- `PublicCloudDownloader.Infrastructure`: HTTP session factory, retry policy,
  safe filesystem writer, logging, clock, and platform services.
- `PublicCloudDownloader.Tests`: unit tests, local HTTP integration tests, and
  provider fixture contract tests.

### 5.1 Provider boundary

Both provider adapters implement one provider-neutral contract with operations
equivalent to:

- Determine whether a link belongs to the adapter.
- Resolve and anonymously preflight the shared item.
- Enumerate a normalized recursive manifest.
- Open an authenticated-by-public-share-session download stream for a manifest
  item. Here `authenticated-by-public-share-session` means cookies or temporary
  share state obtained anonymously from the public page; it never means a user
  account or saved credential.

The provider implementation owns its `HttpClient` handler and in-memory cookie
container for the lifetime of a job. This preserves redirect, cookie, and
temporary URL state between enumeration and download.

Official Google Drive and Microsoft Graph folder-listing APIs require
authorization for the relevant operations. Therefore anonymous folder support
uses provider public-share responses that are not guaranteed stable public API
contracts. This risk is isolated behind the provider boundary. Captured
response fixtures and live opt-in smoke tests detect provider changes without
coupling the rest of the application to response details.

### 5.2 Download coordination

The coordinator consumes the immutable normalized manifest and collision
policy. It creates directories only within the resolved destination root,
downloads with bounded concurrency, and reports provider-neutral progress.
Each file is written to a unique sibling `.partial` path, flushed and closed,
then moved into its final path. Overwrite replaces the existing file only after
the temporary file is complete.

Transient HTTP and network failures are retried a bounded number of times with
backoff. Access denial, unsupported content, invalid provider responses, and
local filesystem permission failures are not retried indefinitely. One file's
failure does not stop unrelated files unless the error invalidates the entire
anonymous provider session.

## 6. Security and Privacy

- Only `https` Google Drive and OneDrive Personal source-link formats are
  accepted.
- Redirects and generated download locations are validated by the owning
  provider adapter; arbitrary schemes and local filesystem URLs are rejected.
- All final filesystem paths are canonicalized and verified to remain beneath
  the selected destination root before any write.
- Anonymous session cookies, temporary download URLs, resource keys, and share
  tokens exist in memory for the job and are never persisted.
- Logs redact source query values and temporary download URLs. They record the
  provider, safe local relative paths, status, sizes, timings, and diagnostic
  categories needed for support.
- The app never asks for or stores Google/Microsoft credentials.
- The app does not execute downloaded files or automatically open downloaded
  content.

## 7. Packaging and Portability

One self-contained Windows x64 publish payload produces:

- `PublicCloudDownloader-v1.0.0-Setup.exe`
- `PublicCloudDownloader-v1.0.0-win-x64.zip`

The Inno Setup installer is per-user and does not request Administrator
privileges. Its default directory is:

`%LocalAppData%\Programs\PublicCloudDownloader`

The user may select another writable installation folder. The installer creates
a Start Menu shortcut, offers an optional Desktop shortcut, registers a normal
uninstaller, and does not install a service, startup entry, or scheduled task.

The application itself has no runtime registry dependency. `data` and `logs`
directories live beside the executable, so the complete installed folder can
be copied to another writable directory or removable drive and run there. The
portable ZIP contains the same application payload without installer metadata.
Uninstalling removes installed program/runtime files but never removes files in
download destinations selected by the user.

## 8. Error Handling

Errors use plain English, identify the failing phase or relative path, and give
the next useful action. Required categories include:

- Unsupported or incomplete link.
- Unsupported OneDrive for Business or SharePoint link.
- Private, expired, or sign-in-required share.
- Download disabled by the owner.
- Provider public-page format changed or could not be interpreted.
- Network unavailable, timeout, throttling, or temporary server failure.
- Insufficient disk space.
- Destination not writable or path too long.
- File changed or disappeared after manifest creation.
- Cancellation.

Provider parsing failures must not be mislabeled as a private link. They use a
diagnostic message stating that the public page could not be processed and that
an application update may be required.

## 9. Testing and Verification

Automated verification includes:

- Link parsing for supported and rejected Google Drive, OneDrive Personal,
  OneDrive for Business, SharePoint, malformed, incomplete, and non-HTTPS URLs.
- Manifest recursion, pagination, shortcut traversal, cycle detection, export
  descriptors, Windows path normalization, deterministic name collisions, and
  destination-root containment.
- Collision analysis and all three user policies.
- Local HTTP integration tests for redirects, cookies, expiring URLs, retries,
  throttling, unknown sizes, interruption, cancellation, partial cleanup, and
  safe overwrite.
- Provider contract tests using sanitized captured public response fixtures.
- Opt-in live smoke tests using test-owned public Google Drive and OneDrive
  Personal shares. These are not required for offline unit-test success.
- WPF view-model/UI behavior tests for disabled/enabled Download state, dialogs,
  progress states, cancellation, accessibility names, and keyboard navigation.
- Release scanning proving that the repository and payload contain no rclone
  binary, OAuth/account implementation, sync command, upload engine, or cloud
  destination.
- Canonical-version consistency tests across assemblies, UI, ZIP, and installer.
- Silent per-user installation to a test directory, file/version/shortcut
  checks, application launch, uninstall, and verification that external user
  downloads remain untouched.
- Portable ZIP extraction, relocation, launch, and write-access verification.
- Visual QA of the approved Layout A Revision 2 at supported Windows scaling
  levels.

## 10. Acceptance Criteria

The design is complete when the implementation can demonstrate all of the
following:

1. A public Google Drive folder with nested content downloads without login to
   `Destination\Source folder name` with its hierarchy retained.
2. A public OneDrive Personal folder does the same.
3. Supported single public files download directly into the selected base.
4. Private and download-disabled links show a warning before destination output
   is created.
5. The Download button stays disabled for incomplete links or invalid local
   destinations.
6. Existing files cannot be changed without explicit `Overwrite existing`
   confirmation.
7. Cancelling or failing a download never presents an incomplete file as a
   completed final file.
8. The app contains no accounts, OAuth, upload, sync, cloud destination, or
   rclone dependency.
9. Both installer EXE and portable ZIP are produced from one versioned payload.
10. The installed folder remains runnable after being copied to another
    writable location.

