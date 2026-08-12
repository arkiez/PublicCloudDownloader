#ifndef AppVersion
  #error AppVersion must be supplied by package.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by package.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be supplied by package.ps1
#endif

[Setup]
AppId={{8D802388-7367-4D1A-A5B7-78EDFA6DD4E9}
AppName=Public Cloud Downloader
AppVersion={#AppVersion}
AppPublisher=Public Cloud Downloader
DefaultDirName={localappdata}\Programs\PublicCloudDownloader
DefaultGroupName=Public Cloud Downloader
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=PublicCloudDownloader-v{#AppVersion}-Setup
OutputDir={#OutputDir}
SetupIconFile={#PublishDir}\PublicCloudDownloader.ico
UninstallDisplayIcon={app}\PublicCloudDownloader.exe
WizardStyle=modern
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Public Cloud Downloader"; Filename: "{app}\PublicCloudDownloader.exe"
Name: "{autodesktop}\Public Cloud Downloader"; Filename: "{app}\PublicCloudDownloader.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\PublicCloudDownloader.exe"; Description: "Launch Public Cloud Downloader"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\data"
Type: filesandordirs; Name: "{app}\logs"
