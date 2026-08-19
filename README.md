# Public Cloud Downloader

**Public Cloud Downloader** is a lightweight Windows desktop utility for downloading public files and folders from **Google Drive** and **OneDrive Personal** without signing in.

It downloads folder contents directly instead of waiting for the cloud service to prepare a ZIP archive first, with concurrent transfers, real-time progress, completion notifications, and built-in updates from GitHub Releases.

## Highlights

- No account sign-in required
- Download public files or recursive folders directly
- Avoid waiting for Google Drive or OneDrive to prepare a ZIP archive
- Download multiple files concurrently with per-file and overall progress
- Export Google Docs, Sheets, and Slides as `.docx`, `.xlsx`, and `.pptx`
- Existing-file options: **Skip**, **Overwrite**, or **Cancel**
- Safe `.partial` writes to reduce incomplete or corrupted output
- Windows notification immediately when a download completes successfully
- Automatic update checks through GitHub Releases
- Portable Windows x64 ZIP — no installer required

## Download

Get the latest stable version from **GitHub Releases**:

https://github.com/arkiez/PublicCloudDownloader/releases/latest

Download the file named:

```text
PublicCloudDownloader-vX.Y.Z-win-x64.zip
```

## Getting started

1. In Google Drive or OneDrive Personal, set the item to **Anyone with the link**.
2. Open `PublicCloudDownloader.exe`.
3. Paste the public file or folder link.
4. Choose a local destination folder.
5. Select **Download**.

The app checks public access before creating files. A shared folder keeps its folder structure under the selected destination, while a shared single file is saved directly there.

## Download progress and completion

The progress window shows overall percentage, completed file count, and the files currently being downloaded. When a job completes successfully, the window changes to **Download complete** and a non-blocking Windows notification is shown immediately — you do not need to close the progress window first.

Cancelled downloads and jobs completed with errors do not generate a successful-completion notification.

## Updates

After startup, the app checks the latest stable GitHub Release in the background. Normal downloading remains available if GitHub or the network cannot be reached.

- **Check for updates** performs a manual check.
- **Update vX.Y.Z** appears only when a newer stable version is available.
- If the installed version is current, the Update button stays hidden.
- **Update now** downloads the release ZIP, verifies its SHA-256 digest, stages the files, updates the application, and restarts it.
- Existing `data` and `logs` folders are preserved during updates.

## Supported services

| Service | Support |
|---|---|
| Google Drive public files | Yes |
| Google Drive public folders | Yes |
| Google Docs / Sheets / Slides export | Yes |
| OneDrive Personal public files | Yes |
| OneDrive Personal public folders | Yes |
| OneDrive Business | No |
| SharePoint | No |

## Privacy and safety

Public Cloud Downloader has no account manager, OAuth sign-in, upload, sync, cloud destination, or local-source mode. Anonymous cookies, tokens, resource keys, and temporary download URLs stay in memory and are not written to logs. No GitHub credential is bundled with the application.

New downloads are written to sibling `.partial` files first. An existing destination file is replaced only after the new copy completes successfully. Failed or cancelled partial files are removed.

## Verify a downloaded release

Each GitHub Release includes `SHA256SUMS.txt`. To verify the ZIP on Windows PowerShell:

```powershell
Get-FileHash "PublicCloudDownloader-vX.Y.Z-win-x64.zip" -Algorithm SHA256
```

Compare the returned hash with the value in `SHA256SUMS.txt` from the same release.

## Portable use

Keep the complete application folder together when moving the app between locations. Runtime `data` and `logs` folders are stored beside the executable. To remove the application, close it and delete its application folder; files downloaded elsewhere are not removed.

## Compatibility note

Cloud providers can change public-share pages and anonymous download endpoints. If the app reports an unrecognized provider response, check for an application update. Public access can also be revoked by the file owner at any time.

## Build from source

Requires the .NET 8 SDK:

```powershell
./scripts/version-test.ps1
dotnet test PublicCloudDownloader.sln -c Release
./scripts/package.ps1
```

The canonical product version is defined in `Version.props`. Release requirements and verification evidence are documented under `docs/requirements/`.

## License

Public Cloud Downloader is released under the [MIT License](LICENSE).

---

Created by **Arkie'z K. Khositkhanawut**
