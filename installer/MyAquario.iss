; MyAquario - instalador (Inno Setup 6)
; Gerar: build.ps1 (ou ISCC.exe installer\MyAquario.iss depois de compilar em build\MyAquario)
;
; Instala so para o usuario (sem pedir administrador). Os pacotes graficos Normal/Ultra
; NAO vao no instalador: o programa baixa do GitHub Releases quando a pessoa escolhe.

#define AppName "MyAquario"
#define AppVersion "0.1.1"
#define AppExe "MyAquario.exe"

[Setup]
AppId={{B4E2A7C1-5D3F-4A8E-9C61-2F7D0E8B3A94}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=SanMobile
AppPublisherURL=https://github.com/SandiehMoreira/MyAquario
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
PrivilegesRequired=lowest
WizardStyle=modern
SetupIconFile=..\src\MyAquario\assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
OutputDir=..\build
OutputBaseFilename=MyAquario-Setup
Compression=lzma2/ultra64
SolidCompression=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Messages]
ptbr.WelcomeLabel2=Este assistente vai instalar o [name/ver] no seu computador.%n%nUm aquario de verdade na sua area de trabalho: cuide do seu betta, alimente, limpe a agua e conquiste aquarios maiores com novos peixes.%n%nOs graficos Normal e Ultra podem ser baixados depois, pelas Configuracoes.

[Tasks]
Name: "startup"; Description: "Abrir o aquario junto com o Windows (recomendado: ele fica na area de trabalho)"; GroupDescription: "Opcoes:"
Name: "desktopicon"; Description: "Criar atalho na Area de Trabalho"; GroupDescription: "Opcoes:"; Flags: unchecked

[Files]
Source: "..\build\MyAquario\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\build\MyAquario\{#AppExe}.config"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MyAquario"; ValueData: """{app}\{#AppExe}"""; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o MyAquario agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Pacotes graficos baixados. O save do peixe (save.json) fica, caso a pessoa reinstale.
Type: filesandordirs; Name: "{localappdata}\MyAquario\packs"

[Code]
// Fecha o aquario antes de atualizar/desinstalar. Filtra pelo nome exato do exe,
// entao nao acerta o proprio instalador (MyAquario-Setup.exe).
procedure FecharMyAquario();
var
  Code: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  FecharMyAquario();
  Result := '';
end;

function InitializeUninstall(): Boolean;
begin
  FecharMyAquario();
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // A opcao "abrir com o Windows" tambem pode ter sido ligada pelo proprio app.
  if CurUninstallStep = usPostUninstall then
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MyAquario');
end;
