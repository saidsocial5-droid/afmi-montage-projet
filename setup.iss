#define MyAppName "AFMI Montage Projet"
; Doit correspondre à <Version> dans "AFMI Montage projet 2.csproj" (vérifié par build_and_sign.ps1 et par les tests)
#define MyAppVersion "1.9.20"
#define MyAppPublisher "Association Forêt Modèle d'Ifrane"
#define MyAppExeName "AFMI Montage projet 2.exe"
#define PublishDir "bin\Release\net9.0-windows\win-x64\publish"

[Setup]
SetupIconFile=setup_icon.ico
; Mise à jour silencieuse depuis l'application (GitHub, ADR 26) : l'installateur ferme l'application ouverte puis la relance
AppMutex=AFMI_Montage_Projet_SingleInstance_Mutex
CloseApplications=yes
RestartApplications=yes
UsePreviousPrivileges=yes
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} — montage de projets de développement
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
; NOTE: AppId identifie l'application pour les mises à niveau : ne jamais le modifier (les installations existantes ne seraient plus mises à jour).
AppId={{5C1B2A3D-4E5F-6G7H-8I9J-0K1L2M3N4O5P}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; Permet à l'utilisateur de choisir entre une installation pour tous les utilisateurs (Admin) ou juste lui-même
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=Installer
OutputBaseFilename=AFMIMontageProjet_Setup_v{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
; Inno Setup inclut l'arabe par défaut avec RTL
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Fichiers de l'application (Générés par dotnet publish)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.WebView2\*,*.WebView2"
; Bootstrapper WebView2 (doit être téléchargé avant compilation)
Source: "MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Exécute l'installation de WebView2 en mode silencieux si nécessaire
Filename: "{tmp}\MicrosoftEdgeWebview2Setup.exe"; Parameters: "/silent /install"; Flags: waituntilterminated; Check: NeedsWebView2
; Lance l'application à la fin de l'installation
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Fonction pour vérifier si WebView2 est déjà installé (vérifie le registre Windows)
function NeedsWebView2: Boolean;
var
  version: String;
begin
  // Vérifie l'installation pour tous les utilisateurs (Machine)
  if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', version) then
  begin
    Result := False;
    Exit;
  end;
  // Vérifie l'installation pour l'utilisateur actuel (Current User)
  if RegQueryStringValue(HKEY_CURRENT_USER, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', version) then
  begin
    Result := False;
    Exit;
  end;
  // S'il n'est pas trouvé, l'installation est requise
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  MsgBoxResult: Integer;
begin
  // A l'étape post-désinstallation, demander si on veut supprimer les données locales
  if CurUninstallStep = usPostUninstall then
  begin
    MsgBoxResult := MsgBox(
      'Voulez-vous également supprimer toutes les données de l''application (Base de données locale, paramètres) ?' + #13#10 +
      'Choisissez OUI pour tout effacer, ou NON pour conserver vos projets.',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2);
      
    if MsgBoxResult = IDYES then
    begin
      DelTree(ExpandConstant('{userappdata}\AFMI_Montage_Projet'), True, True, True);
      DelTree(ExpandConstant('{localappdata}\AFMI_Montage_Projet'), True, True, True);
    end;
  end;
end;
