; ============================================================
; Douze Assistance — Inno Setup Script
; Usage: ISCC.exe DouzeAssistance.iss /DMyAppVersion=2.0.0
; ============================================================

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName      "Douze Assistance"
#define MyAppPublisher "Douze Assistance"
#define MyAppURL       "https://github.com/RomainRssl/DouzeAssistanceOverlay"
#define MyAppExeName   "DouzeAssistance.exe"
; Dossier publish framework-dependent (relatif au script)
#define MySourceDir    "..\dist"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=..\
OutputBaseFilename=DouzeAssistance_Setup_v{#MyAppVersion}
SetupIconFile=..\LMUOverlay\Resources\app.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; Exécutable principal
Source: "{#MySourceDir}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Toutes les DLLs .NET (framework-dependent)
Source: "{#MySourceDir}\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; Fichiers de config runtime
Source: "{#MySourceDir}\*.json"; DestDir: "{app}"; Flags: ignoreversion
; OpenVR (optionnel — copié seulement si présent)
Source: "{#MySourceDir}\openvr_api.dll"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// ============================================================
// Vérification du prérequis .NET 8 Desktop Runtime
// ============================================================

function IsDotNet8DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
  BasePath: String;
begin
  Result := False;
  // Chercher un dossier "8.*" dans le répertoire des runtimes partagés .NET
  BasePath := ExpandConstant('{pf64}\dotnet\shared\Microsoft.WindowsDesktop.App\');
  if FindFirst(BasePath + '8.*', FindRec) then
  begin
    Result := True;
    FindClose(FindRec);
  end;
end;

function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;

  if not IsDotNet8DesktopInstalled() then
  begin
    if MsgBox(
      '.NET 8 Desktop Runtime est requis pour Douze Assistance.' + #13#10 + #13#10 +
      'Cliquez OK pour ouvrir la page de téléchargement Microsoft,' + #13#10 +
      'puis relancez l''installation après avoir installé .NET 8.' + #13#10 + #13#10 +
      'Annuler pour quitter.',
      mbConfirmation,
      MB_OKCANCEL
    ) = IDOK then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
    Result := False;
  end;
end;
