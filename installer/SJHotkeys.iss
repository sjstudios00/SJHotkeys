#ifndef AppVersion
#define AppVersion "1.1.0"
#endif

#ifndef AppExePath
#define AppExePath "..\artifacts\installer\app\SJ Hotkeys.exe"
#endif

[Setup]
AppId={{BFF28D27-3BA9-4B2C-B165-90173F96E702}
AppName=SJ Hotkeys
AppVersion={#AppVersion}
AppPublisher=SJ Studios
AppPublisherURL=https://github.com/sjstudios00/SJHotkeys
AppSupportURL=https://github.com/sjstudios00/SJHotkeys/issues
AppUpdatesURL=https://github.com/sjstudios00/SJHotkeys/releases
DefaultDirName={localappdata}\Programs\SJ Studios\SJ Hotkeys
DefaultGroupName=SJ Hotkeys
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=SJ-Hotkeys-Setup-v{#AppVersion}-win-x64
SetupIconFile=..\Assets\sj-hotkeys.ico
UninstallDisplayIcon={app}\SJ Hotkeys.exe
UninstallDisplayName=SJ Hotkeys
Uninstallable=yes
CloseApplications=yes
RestartApplications=no
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
SetupLogging=yes
LicenseFile=..\LICENSE
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#AppExePath}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\SJ Hotkeys"; Filename: "{app}\SJ Hotkeys.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\SJ Hotkeys"; Filename: "{app}\SJ Hotkeys.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SJ Hotkeys.exe"; Description: "Launch SJ Hotkeys"; Flags: postinstall nowait skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand, InstalledExe: String;
begin
  if CurUninstallStep = usUninstall then begin
    InstalledExe := '"' + ExpandConstant('{app}\SJ Hotkeys.exe') + '"';
    if RegQueryStringValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'SJHotkeys', StartupCommand) and
      (CompareText(Copy(StartupCommand, 1, Length(InstalledExe)), InstalledExe) = 0) then
      RegDeleteValue(HKEY_CURRENT_USER,
        'Software\Microsoft\Windows\CurrentVersion\Run', 'SJHotkeys');
  end;
end;
