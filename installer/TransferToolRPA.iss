; =============================================================================
; TransferToolRPA — script de instalação (Inno Setup)
; -----------------------------------------------------------------------------
; Gera um único Setup.exe (tipo GIMP/PowerPoint) a partir da pasta de publish.
; O instalador cria o atalho na Área de Trabalho e no Menu Iniciar.
;
; Pré-requisitos:
;   1. Gerar a pasta de publish (self-contained, SEM single-file):
;        dotnet publish TransferToolRPA.csproj -c Release -r win-x64 --self-contained true -o ./publish
;   2. Instalar o Inno Setup: https://jrsoftware.org/isinfo.php
;   3. Compilar este script (botão "Compile" do editor, ou):
;        ISCC.exe installer\TransferToolRPA.iss
;
; O instalador embute o runtime .NET + driver node.exe + browsers Chromium/ffmpeg,
; por isso o Setup.exe final fica grande (~250–560 MB). É o mesmo trade-off de
; instaladores como GIMP/PowerPoint, que também embutem seus motores/bibliotecas.
; =============================================================================

#define MyAppName "TransferTool RPA"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "VitorRosaDev"
#define MyAppExeName "TransferToolRPA.exe"

[Setup]
AppId={{B7E4A2C6-9D1F-4E8B-A3C5-2F9D0E7B6A51}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputBaseFilename=TransferToolRPA-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\icon.ico
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
LicenseFile=..\LICENSE

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "&Criar atalho na Área de Trabalho"; GroupDescription: "Atalhos adicionais:"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "&Executar {#MyAppName}"; Flags: nowait postinstall skipifsilent
