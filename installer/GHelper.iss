; ---------------------------------------------------------------------------
;  G-Helper - Inno Setup script
; ---------------------------------------------------------------------------
;  Builds a single setup.exe that:
;    - installs the single-file GHelper.exe to a per-user Programs folder
;      (no UAC prompt: the app runs asInvoker)
;    - creates a Start Menu shortcut (and an optional Desktop shortcut)
;    - writes the standard Add/Remove-Programs / uninstall registry entry
;    - checks for the .NET 8 Desktop Runtime at startup and offers a link if
;      it is missing (the app is framework-dependent, like the official build)
;
;  Build (run from the repo root, or adjust SOURCE below):
;    "C:\Users\<you>\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer\GHelper.iss
;
;  This installer is ADDITIVE: it does not touch the app's own auto-start
;  mechanism (a Task Scheduler logon task managed by the in-app "Run on
;  Startup" checkbox). Adding a registry Run-key here would double-launch.
; ---------------------------------------------------------------------------

#define GHelperVersion      "0.257"
#define PublishDir          "..\app\bin\x64\Release\net8.0-windows\win-x64\publish"

[Setup]
; Stable per-app GUID so upgrades replace in place (do NOT change between versions).
AppId={{8F4B2C7A-1D9E-4A6C-B3F2-7E5A9C0D1234}
AppName=G-Helper
AppVersion={#GHelperVersion}
AppVerName=G-Helper {#GHelperVersion}
AppPublisher=G-Helper
AppPublisherURL=https://g-helper.com
AppSupportURL=https://github.com/seerge/g-helper/issues
AppUpdatesURL=https://github.com/seerge/g-helper/releases
AppContact=https://github.com/seerge/g-helper

; Per-user install (matches the app's asInvoker manifest - no elevation needed).
; {autopf} resolves to %LOCALAPPDATA%\Programs\GHelper under lowest privileges.
DefaultDirName={autopf}\GHelper
DefaultGroupName=G-Helper
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; x64 only.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=yes
UninstallDisplayIcon={app}\GHelper.exe
UninstallDisplayName=G-Helper

; Tight compression.
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
OutputDir=Output
OutputBaseFilename=GHelperSetup
RestartIfNeededByRun=no

; Bundle the version info into the setup.exe's file properties.
VersionInfoVersion={#GHelperVersion}.0
VersionInfoProductVersion={#GHelperVersion}.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The single-file, framework-dependent build produced by:
;   dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --no-self-contained
Source: "{#PublishDir}\GHelper.exe"; DestDir: "{app}"; Flags: ignoreversion
; Ship the icon too so shortcuts display it correctly even if the host resolves
; the icon from the exe lazily (harmless if redundant).
Source: "..\app\favicon.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\G-Helper"; Filename: "{app}\GHelper.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GHelper.exe"; Comment: "Lightweight control tool for Asus laptops"
Name: "{group}\Uninstall G-Helper"; Filename: "{uninstallexe}"
Name: "{autodesktop}\G-Helper"; Filename: "{app}\GHelper.exe"; WorkingDir: "{app}"; IconFilename: "{app}\GHelper.exe"; Tasks: desktopicon

[Run]
; Optional: launch right after install.
Filename: "{app}\GHelper.exe"; Description: "{cm:LaunchProgram,G-Helper}"; Flags: nowait postinstall skipifsilent runasoriginaluser

; --- .NET 8 Desktop Runtime check -----------------------------------------
; Framework-dependent => the app needs the .NET 8 Desktop Runtime. We probe for
; any installed 8.x WindowsDesktop shared-framework folder (the most reliable
; signal; the registry layout varies across installer versions). If missing,
; warn with a download link and let the user decide - we do NOT silently
; install the runtime.
[Code]
function IsDotNet8DesktopRuntimeInstalled: Boolean;
var
  BaseDir: String;
  FindRec: TFindRec;
begin
  Result := False;
  // The runtime is installed machine-wide under %ProgramFiles%\dotnet\shared.
  BaseDir := ExpandConstant('{pf}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(BaseDir) then Exit;
  // Any 8.* subfolder means a .NET 8 desktop runtime is present.
  if FindFirst(AddBackslash(BaseDir) + '*', FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          if (FindRec.Name <> '.') and (FindRec.Name <> '..') and (Pos('8.', FindRec.Name) = 1) then
          begin
            Result := True;
            Break;
          end;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet8DesktopRuntimeInstalled then
  begin
    if MsgBox(
        'G-Helper requires the .NET 8.0 Desktop Runtime, which does not appear to be installed.' #13#10 #13#10 +
        'You can continue installing now, but G-Helper will not run until the runtime is installed.' #13#10 #13#10 +
        'Open the download page now?',
        mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
  end;
end;
