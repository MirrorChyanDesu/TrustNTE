#define MyAppName "TrustNTE"
#define MyAppVersion "0.0.10"
#define MyAppExeName "TrustNTE.exe"

[Setup]
AppId={{8F3C1A2E-6B7D-4E5A-9C0F-1D2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={localappdata}\{#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename=安装版（Installer）v{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

; 👇 这一行是精髓！控制面板里显示的名字会非常专业
UninstallDisplayName={#MyAppName} v{#MyAppVersion} (异环自动化工具)

PrivilegesRequired=admin

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Files]
Source: "D:\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
; 👇 开始菜单快捷方式，玩家看到的是“卸载 TrustNTE”
Name: "{autoprograms}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Run {#MyAppName}"; Flags: nowait postinstall skipifsilent runascurrentuser

; 👇 卸载时彻底清理 AppData 中的配置和缓存，不留垃圾（强迫症福音）
[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"