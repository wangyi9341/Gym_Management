; GYM力量健身管理系统 - Inno Setup 安装脚本
; 生成的安装包：dist\GYM力量健身管理系统_Setup_*.exe
;
; 依赖：安装 Inno Setup 6（https://jrsoftware.org/isinfo.php）
; 构建方式：运行 scripts\build-installer.ps1

#define MyAppName "GYM力量健身管理系统"
#define MyAppPublisher "GYM"
#define MyAppURL "https://example.local/"
#define MyAppExeName "GymManager.App.exe"

; 外部可通过 ISCC /DMyRuntime=win-x64 /DMyConfiguration=Release 传入
#ifndef MyRuntime
  #define MyRuntime "win-x64"
#endif
#ifndef MyConfiguration
  #define MyConfiguration "Release"
#endif

#define MyPublishDir "..\\..\\src\\GymManager.App\\bin\\" + MyConfiguration + "\\net8.0-windows\\" + MyRuntime + "\\publish"
#define MyAppVersion GetFileVersion(AddBackslash(MyPublishDir) + MyAppExeName)

[Setup]
AppId={{D2C8F6C3-0D7A-4B8E-9C84-3B2C5A5D8B6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 选择“全局安装”：安装到 Program Files（需要管理员权限）
PrivilegesRequired=admin
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}

; 根据发布的 Runtime 决定是否启用 64 位安装模式
#if (MyRuntime == "win-x64") || (MyRuntime == "win-arm64")
ArchitecturesInstallIn64BitMode=x64 arm64
#endif

; UI / 输出
WizardStyle=modern
Compression=lzma2
SolidCompression=yes
OutputDir=..\..\dist
OutputBaseFilename={#MyAppName}_Setup_{#MyAppVersion}_{#MyRuntime}

; 使用 EXE 作为卸载图标（若 EXE 无自带图标，可后续再补）
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务"; Flags: unchecked

[Files]
; 单文件发布时 publish 目录应只有 1 个 exe
Source: "{#MyPublishDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "运行 {#MyAppName}"; Flags: nowait postinstall skipifsilent
