PUBLIC CLOUD DOWNLOADER {{VERSION}}
=================================

Public Cloud Downloader downloads public Google Drive and OneDrive Personal
files and folders without an account. Folder contents are downloaded directly,
helping avoid waiting for the cloud service to prepare a ZIP archive first.

KEY FEATURES
- No account sign-in required
- Public files and recursive folders
- Multiple concurrent downloads with real-time overall and per-file progress
- Google Docs, Sheets, and Slides export to DOCX, XLSX, and PPTX
- Existing-file choices: Skip, Overwrite, or Cancel
- Safe .partial file writes
- Immediate Windows notification after a successful download
- Built-in update checks through GitHub Releases
- Portable Windows x64 package; no installer required

GET STARTED
1. In Google Drive or OneDrive Personal, set General access to
   "Anyone with the link".
2. Open PublicCloudDownloader.exe.
3. Paste the public file or folder link.
4. Choose an existing local destination, for example D:\Downloads.
5. Select Download. Public access is checked before files are created.

A shared folder keeps its folder structure inside the selected destination.
A shared single file is saved directly in the selected destination.

DOWNLOAD PROGRESS AND COMPLETION
The progress window shows overall percentage, completed file count, and files
currently being downloaded. When a job completes successfully, the window shows
"Download complete" and a non-blocking Windows notification appears immediately.
You do not need to close the progress window first.

Cancelled jobs and jobs completed with errors do not show a successful-completion
notification.

UPDATES
The app checks GitHub Releases for a newer stable version after startup without
blocking normal downloads. Select "Check for updates" in the footer to check
manually. When a newer version is available, an "Update vX.Y.Z" button appears.
When the installed version is current, that button stays hidden.

Select "Update now" to download the release ZIP, verify its SHA-256 digest,
stage the new files, replace the application payload, and restart the app.
Select "Later" to keep using the current version. Update failures do not disable
normal cloud downloading. Existing data and logs folders are preserved.

SUPPORTED SERVICES
- Google Drive public files and recursive folders
- OneDrive Personal public files and recursive folders
- Google Docs, Sheets, and Slides export to DOCX, XLSX, and PPTX

OneDrive Business and SharePoint are not supported.

PRIVACY AND FILE SAFETY
The app has no account sign-in, upload, sync, cloud destination, or local-source
mode. Anonymous cookies, tokens, resource keys, and temporary download URLs stay
in memory and are not written to logs. No GitHub credential is bundled with the
application.

New downloads are written to sibling .partial files first. Existing destination
files are replaced only after the new copy completes successfully. Failed and
cancelled partial files are removed.

VERIFY A RELEASE
Each GitHub Release includes SHA256SUMS.txt. In Windows PowerShell, run:

  Get-FileHash "PublicCloudDownloader-vX.Y.Z-win-x64.zip" -Algorithm SHA256

Compare the returned value with SHA256SUMS.txt from the same release.

PORTABLE USE AND REMOVAL
Keep the complete application folder together when moving the app. Runtime data
and logs folders are stored beside the EXE. To remove the app, close it and
delete its application folder. Files downloaded to other folders are not deleted.

COMPATIBILITY
Cloud providers can change public-share pages and anonymous download endpoints.
If the app reports an unrecognized provider response, check for an update.
Public access can also be revoked by the file owner at any time.
