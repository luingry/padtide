#ifndef AppVersion
  #error AppVersion must come from VERSION via scripts/build-installer.ps1
#endif
#define AppMutexName "Local\PadTide.SingleInstance"

[Setup]
AppId={{7C2E5B0A-6E4F-4B7E-9A51-3D8F1C60A2D4}
AppName=PadTide
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
AppPublisher=luingry
AppPublisherURL=https://github.com/luingry/padtide
DefaultDirName={localappdata}\Programs\PadTide
DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=yes
DisableWelcomePage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir=..\artifacts
OutputBaseFilename=PadTide-Setup-{#AppVersion}
SetupIconFile=..\assets\padtide.ico
UninstallDisplayIcon={app}\PadTide.exe
UninstallDisplayName=PadTide
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=no
RestartApplications=no
AppMutex={#AppMutexName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
Source: "..\src\PadTide\bin\Release\net48\PadTide.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\PadTide"; Filename: "{app}\PadTide.exe"

[Run]
Filename: "{app}\PadTide.exe"; Description: "Abrir o PadTide"; Flags: nowait postinstall skipifsilent
; The in-app updater passes /update=1; --startup keeps the relaunch quiet when the controller is off.
Filename: "{app}\PadTide.exe"; Parameters: "--startup"; Flags: nowait; Check: ShouldRelaunchAfterUpdate

[Registry]
; Created by the "Iniciar com o Windows" setting; removed with the app, and nothing else in the key is touched.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "PadTide"; Flags: uninsdeletevalue dontcreatekey

[Code]
function ShouldRelaunchAfterUpdate(): Boolean;
begin
  Result := ExpandConstant('{param:update|0}') = '1';
end;

procedure CloseRunningApp();
var
  Window: HWND;
  Attempt: Integer;
begin
  Window := FindWindowByWindowName('PadTide.Host');
  if Window <> 0 then
    PostMessage(Window, $0010, 0, 0);
  for Attempt := 1 to 40 do begin
    if not CheckForMutexes('{#AppMutexName}') then
      Exit;
    Sleep(100);
  end;
end;

function InitializeSetup(): Boolean;
begin
  { Inno checks AppMutex before PrepareToInstall, so the app must close before that check. }
  CloseRunningApp();
  Result := not CheckForMutexes('{#AppMutexName}');
end;

function InitializeUninstall(): Boolean;
begin
  CloseRunningApp();
  Result := not CheckForMutexes('{#AppMutexName}');
end;
