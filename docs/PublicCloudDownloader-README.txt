PUBLIC CLOUD DOWNLOADER {{VERSION}}
=================================

Public Cloud Downloader downloads public Google Drive and OneDrive Personal
files and folders without an account.

GET STARTED
1. In Google Drive or OneDrive Personal, set General access to
   "Anyone with the link".
2. Paste the public file or folder link.
3. Choose an existing local destination, for example D:\Downloads.
4. Select Download. The app checks public access before creating files.

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

PRIVACY AND PORTABLE USE
The app has no account sign-in, upload, sync, cloud destination, or local source
mode. Anonymous cookies, tokens, resource keys, and temporary URLs stay in
memory and are not written to logs. Keep the entire application folder together
when using it portably. The data and logs folders are stored beside the EXE.

UNINSTALL
Uninstall removes only the application and its local data/logs. Files downloaded
to other folders are not deleted.

COMPATIBILITY
Cloud providers can change public-share pages and endpoints. If the app reports
an unrecognized response, an application update may be required.
