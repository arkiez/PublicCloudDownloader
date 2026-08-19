PUBLIC CLOUD DOWNLOADER {{VERSION}}
=================================

Public Cloud Downloader downloads public Google Drive and OneDrive Personal
files and folders without an account. It downloads folder contents directly,
helping avoid waiting for the cloud service to prepare a ZIP archive first.

GET STARTED
1. In Google Drive or OneDrive Personal, set General access to
   "Anyone with the link".
2. Paste the public file or folder link.
3. Choose an existing local destination, for example D:\Downloads.
4. Select Download. The app checks public access before creating files.
5. When a download completes successfully, the app keeps the in-app completion
   status and also shows a non-blocking Windows notification.

FOLDER LAYOUT
A folder called "Project Assets" is stored as:
  D:\Downloads\Project Assets\...
A single file is stored directly in the selected destination.

EXISTING FILES AND SAFETY
When files already exist, choose Cancel, Skip existing, or Overwrite existing.
The choice applies to the entire job. New data is written to a sibling .partial
file first. An old file is replaced only after the new file is complete. Failed
and cancelled partial files are removed. The progress window supports Cancel;
failed jobs can be retried.

SUPPORTED SERVICES
- Google Drive public files and recursive folders
- OneDrive Personal public files and recursive folders
- Google Docs, Sheets, and Slides export to DOCX, XLSX, and PPTX

OneDrive Business and SharePoint are not supported.

UPDATES
The app checks GitHub Releases for a newer stable version after startup without
blocking normal downloads. Select "Check for updates" in the footer to check
manually. When a newer version is available, an "Update vX.Y.Z" button appears;
when no update is available, that button stays hidden.

Select "Update now" to download the release ZIP, verify its SHA-256 digest,
stage the new files, replace the application payload, and restart the app.
Select "Later" to keep using the current version. Update failures do not disable
normal cloud downloading. Existing data and logs folders are preserved.

PRIVACY AND PORTABLE USE
The app has no account sign-in, upload, sync, cloud destination, or local source
mode. Anonymous cookies, tokens, resource keys, and temporary URLs stay in
memory and are not written to logs. No GitHub credential is bundled with the
application. Keep the entire application folder together when using it portably.
The data and logs folders are stored beside the EXE.

PORTABLE REMOVAL
Close the app, then delete its complete application folder. Files downloaded to
other folders are not deleted.

COMPATIBILITY
Cloud providers can change public-share pages and endpoints. If the app reports
an unrecognized response, an application update may be required.
