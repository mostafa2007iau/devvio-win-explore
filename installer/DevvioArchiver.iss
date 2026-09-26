; ============================================================
;  Devvio Archiver - Inno Setup script
;  Builds: dist\DevvioArchiver-Setup-1.0.0.exe
;  Prerequisite: run scripts\build.ps1 first (creates ..\stage)
; ============================================================

#define MyAppName "Devvio Archiver"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Devvio"
#define MyAppExeName "DevvioArchiver.App.exe"
#define ShellClsid "{991DE108-BB35-4F0D-B518-466CDEBC7E53}"

[Setup]
AppId={{86352496-D068-44C9-9005-03BB979CCB4E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Devvio Archiver
DefaultGroupName=Devvio Archiver
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=DevvioArchiver-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\stage\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "7-ZIP-LICENSE.txt"; DestDir: "{app}\7z"; Flags: ignoreversion
Source: "lgpl-3.0.txt"; DestDir: "{app}\7z"; Flags: ignoreversion

[Icons]
Name: "{group}\Devvio Archiver"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Devvio Archiver"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; 7-Zip style registration: one handler for all files, folders, folder
; backgrounds and drives. The extension itself decides what to show.
Root: HKLM; Subkey: "SOFTWARE\Classes\*\ShellEx\ContextMenuHandlers\DevvioArchiver"; ValueType: string; ValueData: "{#ShellClsid}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\ShellEx\ContextMenuHandlers\DevvioArchiver"; ValueType: string; ValueData: "{#ShellClsid}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Directory\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"; ValueType: string; ValueData: "{#ShellClsid}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Drive\ShellEx\ContextMenuHandlers\DevvioArchiver"; ValueType: string; ValueData: "{#ShellClsid}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "SOFTWARE\Classes\Drive\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"; ValueType: string; ValueData: "{#ShellClsid}"; Flags: uninsdeletekey
; Fallback lookup path for the helper app
Root: HKLM; Subkey: "SOFTWARE\DevvioArchiver"; ValueType: string; ValueName: "AppPath"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekeyifempty

[Run]
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: "/codebase ""{app}\DevvioArchiver.Shell.dll"""; Flags: runhidden; Check: IsWin64
Filename: "{win}\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"; Parameters: "/codebase ""{app}\DevvioArchiver.Shell.dll"""; Flags: runhidden
; Reload the shell so the new menus appear immediately.
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; Flags: runhidden nowait; Description: "Restart Explorer"

[UninstallRun]
Filename: "{win}\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"; Parameters: "/u ""{app}\DevvioArchiver.Shell.dll"""; Flags: runhidden; RunOnceId: "UnregAsm64"; Check: IsWin64
Filename: "{win}\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe"; Parameters: "/u ""{app}\DevvioArchiver.Shell.dll"""; Flags: runhidden; RunOnceId: "UnregAsm32"
Filename: "{cmd}"; Parameters: "/c taskkill /f /im explorer.exe & start explorer.exe"; Flags: runhidden nowait; RunOnceId: "RestartExplorer"
