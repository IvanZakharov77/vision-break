#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

[Setup]
AppId=VisionBreak.IvanZakharov77
AppName=VisionBreak
AppVersion={#AppVersion}
AppPublisher=IvanZakharov77
AppPublisherURL=https://github.com/IvanZakharov77/vision-break
DefaultDirName={localappdata}\VisionBreak
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=VisionBreak-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
CloseApplications=no
UninstallDisplayIcon={app}\VisionBreak.exe

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\VisionBreak.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\apps.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist uninsneveruninstall

[Icons]
Name: "{userstartup}\VisionBreak"; Filename: "{app}\VisionBreak.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\VisionBreak.exe"; Flags: nowait skipifsilent

[Code]
function StopVisionBreak: Boolean;
var
  ProgramPath: String;
  ResultCode: Integer;
begin
  ProgramPath := ExpandConstant('{app}\VisionBreak.exe');
  Result := True;
  if FileExists(ProgramPath) then
    Result := Exec(ProgramPath, '--stop', ExpandConstant('{app}'),
                   SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopVisionBreak then
    Result := 'Could not stop the running VisionBreak process. Try again after signing out.';
end;

function InitializeUninstall: Boolean;
begin
  Result := StopVisionBreak;
end;
