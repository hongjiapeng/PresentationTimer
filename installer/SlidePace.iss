#define MyAppName "SlidePace"
#define MyAppPublisher "SlidePace contributors"
#define MyAppExeName "SlidePace.App.exe"

#ifndef AppVersion
  #define AppVersion "0.2.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "zh_CN"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "zh_TW"; MessagesFile: "languages\ChineseTraditional.isl"

[Setup]
AppId={{a0d01205-281b-4773-9ef3-216ab3bede1a}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/hongjiapeng/PresentationTimer
AppSupportURL=https://github.com/hongjiapeng/PresentationTimer/issues
DefaultDirName={localappdata}\Programs\SlidePace
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
CloseApplications=yes
CloseApplicationsFilter=SlidePace.App.exe
RestartApplications=no
OutputDir={#OutputDir}
OutputBaseFilename=SlidePace-{#AppVersion}-win-x64-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\SlidePace.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon.ico

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartOnSignIn}"; GroupDescription: "{cm:AdditionalOptions}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SlidePace"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\SlidePace"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon
Name: "{userstartup}\SlidePace"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchSlidePace}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[CustomMessages]
en.CreateDesktopIcon=Create a desktop shortcut
en.AdditionalShortcuts=Additional shortcuts:
en.StartOnSignIn=Start SlidePace when I sign in to Windows
en.AdditionalOptions=Additional options:
en.LaunchSlidePace=Launch SlidePace
zh_CN.CreateDesktopIcon=创建桌面快捷方式
zh_CN.AdditionalShortcuts=附加快捷方式：
zh_CN.StartOnSignIn=登录 Windows 后启动 SlidePace
zh_CN.AdditionalOptions=附加选项：
zh_CN.LaunchSlidePace=启动 SlidePace
zh_TW.CreateDesktopIcon=建立桌面捷徑
zh_TW.AdditionalShortcuts=其他捷徑：
zh_TW.StartOnSignIn=登入 Windows 後啟動 SlidePace
zh_TW.AdditionalOptions=其他選項：
zh_TW.LaunchSlidePace=啟動 SlidePace
