; Instalador do Windows (Inno Setup 6). Instala só para o usuário atual, sem pedir administrador.
#define AppVersion GetEnv("YTSTUDIO_VERSION")
#if AppVersion == ""
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{6F1E4C2B-7A4D-4E0B-9C5E-3B8F2A1D9E70}
AppName=YTStudio
AppVersion={#AppVersion}
AppPublisher=Mateus Kraiewski
DefaultDirName={localappdata}\Programs\YTStudio
DefaultGroupName=YTStudio
PrivilegesRequired=lowest
OutputDir=..\..\dist
OutputBaseFilename=YTStudio-{#AppVersion}-windows-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\YTStudio.exe
; Atualização pelo app: fecha o YTStudio aberto antes de trocar os arquivos.
CloseApplications=force
RestartApplications=no

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\YTStudio"; Filename: "{app}\YTStudio.exe"
Name: "{autodesktop}\YTStudio"; Filename: "{app}\YTStudio.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\YTStudio.exe"; Description: "{cm:LaunchProgram,YTStudio}"; Flags: nowait postinstall skipifsilent
; Na atualização silenciosa feita pelo próprio app, abre o YTStudio de novo no fim.
Filename: "{app}\YTStudio.exe"; Flags: nowait; Check: WizardSilent
