; SnapFind Inno Setup script
; 用法：
;   ISCC.exe /DAppVersion=2.4.6 /DEdition=Rapid /O"releases\installers" src\setup.iss

#ifndef AppVersion
#define AppVersion "2.4.5"
#endif

#ifndef Edition
#define Edition "Rapid"
#endif

#define MySourceDir "src"

#ifndef PublishDir
#define PublishDir MySourceDir + "\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
#endif

[Setup]
AppId=SnapFind-{#Edition}
AppName=SnapFind {#Edition}
AppVersion={#AppVersion}
AppPublisher=SnapFind Developer
DefaultDirName={localappdata}\SnapFind
DefaultGroupName=SnapFind
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
CloseApplications=yes
PrivilegesRequired=lowest
OutputBaseFilename=SnapFindSetup_{#Edition}_v{#AppVersion}
Compression=lzma2/ultra
SolidCompression=yes
AppMutex=SnapFind-SingleInstance-Mutex-Key
WizardStyle=modern
UninstallDisplayIcon={app}\SnapFind.exe

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.StartupProgram=开机自启动 SnapFind
chinesesimplified.OtherTasks=其他任务
english.StartupProgram=Start SnapFind on Windows startup
english.OtherTasks=Other tasks

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartupProgram}"; GroupDescription: "{cm:OtherTasks}"

[Files]
Source: "{#PublishDir}\SnapFind.exe"; DestDir: "{app}"; Flags: ignoreversion
#if Edition == "Rapid"
Source: "{#MySourceDir}\..\libs\rapid\*"; DestDir: "{app}\libs\rapid"; Flags: recursesubdirs createallsubdirs ignoreversion
#else
Source: "{#MySourceDir}\..\libs\*"; DestDir: "{app}\libs"; Flags: recursesubdirs createallsubdirs ignoreversion
#endif

[Icons]
Name: "{group}\SnapFind {#Edition}"; Filename: "{app}\SnapFind.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\SnapFind {#Edition}"; Filename: "{app}\SnapFind.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SnapFind"; ValueData: """{app}\SnapFind.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\SnapFind.exe"; Description: "{cm:LaunchProgram,SnapFind}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\cache"
