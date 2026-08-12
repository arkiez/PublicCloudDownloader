# Public Cloud Downloader

Public Cloud Downloader is an English-only Windows desktop utility for downloading public Google Drive and OneDrive Personal files and folders without signing in.

## What it supports

- Public Google Drive files and recursive folders
- Public OneDrive Personal files and recursive folders
- Google Docs, Sheets, and Slides exported as `.docx`, `.xlsx`, and `.pptx`
- Portable ZIP and a per-user Windows installer

The cloud item must use **General access: Anyone with the link**. OneDrive Business and SharePoint are not supported.

## How downloads are stored

A shared folder named `Project Assets` saved to `D:\Downloads` becomes:

```text
D:\Downloads\Project Assets\...
```

A shared single file is saved directly in the selected destination. If files already exist, choose once between **Skip existing**, **Overwrite existing**, or **Cancel**. Downloads use temporary `.partial` files; an existing file is replaced only after the new copy finishes successfully.

## Privacy and portability

There is no account manager, OAuth sign-in, upload, sync, cloud destination, or local source mode. Anonymous session tokens, cookies, resource keys, and temporary download URLs stay in memory and are excluded from logs. Runtime `data` and `logs` folders are stored beside the executable, so copying the complete application folder preserves portable operation.

Uninstalling removes only the installed application folder and its local `data`/`logs`; downloaded files in destinations such as `D:\Downloads` are not removed.

## Compatibility note

Public-share pages and anonymous service endpoints can change. A response-format warning means an application update may be required. Public access may also be disabled by a file owner at any time.

## Build

Requires the .NET 8 SDK and Inno Setup 6:

```powershell
./scripts/version-test.ps1
dotnet test PublicCloudDownloader.sln -c Release
./scripts/package.ps1
```

The canonical product version is defined in `Version.props`.
