#define MyAppName "Coursia"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "Coursia"
#define PublishDir "bin\Release\net9.0-windows\win-x64\publish"

[Setup]
AppId={{B9E7E8C1-5F59-4B90-9B35-7A1A0C510001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\Coursia
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=installer-output
SetupIconFile=icone.ico
OutputBaseFilename=Coursia-Setup-v0.2
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\Coursia.exe

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Coursia"; Filename: "{app}\Coursia.exe"
Name: "{autodesktop}\Coursia"; Filename: "{app}\Coursia.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Coursia.exe"; Description: "Lancer Coursia"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Les cours et préférences dans %LocalAppData%\Coursia sont conservés volontairement.
Type: filesandordirs; Name: "{app}"
