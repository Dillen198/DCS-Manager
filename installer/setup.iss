; DCS Manager - Inno Setup Script
; Requires Inno Setup 6+ from https://jrsoftware.org/isinfo.php
;
; Build: dotnet publish first, then compile this script.
; Usage:
;   dotnet publish src\DCSManager.App\DCSManager.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish\
;   iscc installer\setup.iss

#define AppName "DCS Manager"
#define AppVersion "1.0.0"
#define AppPublisher "DCS Manager"
#define AppURL "https://github.com/dillen198/dcs-plugin-manager"
#define AppExeName "DCSManager.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=no
LicenseFile=
OutputDir=dist
OutputBaseFilename=DCSManager-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}
MinVersion=10.0
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start DCS Manager automatically with Windows (system tray)"; GroupDescription: "Startup:"; Flags: checked

[Files]
Source: "{#PublishDir}\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\catalog\plugins.json"; DestDir: "{app}\catalog"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "DCSManager"; ValueData: """{app}\{#AppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
