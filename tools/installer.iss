; Lo compila tools/publish.ps1, que define AppVersion y FileVersion a partir del csproj

[Setup]
; No cambiar AppId: es lo que hace que una versión nueva actualice a la anterior
AppId={{8315C498-C122-4471-BC11-CF0091B46591}
AppName=OpenWinBox
AppVersion={#AppVersion}
AppPublisher=Yosefh
VersionInfoVersion={#FileVersion}
; Por usuario y sin administrador: {autopf} es %LOCALAPPDATA%\Programs
PrivilegesRequired=lowest
DefaultDirName={autopf}\OpenWinBox
DisableDirPage=yes
DisableProgramGroupPage=yes
DefaultGroupName=OpenWinBox
; Igual que TargetPlatformMinVersion en el csproj
MinVersion=10.0.17763
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; El mutex de instancia única de App.xaml.cs: pide cerrar la app antes de instalar o desinstalar
AppMutex=OpenWinBox.SingleInstance
CloseApplications=no
SetupIconFile=..\src\OpenWinBox\Assets\OpenWinBox.ico
UninstallDisplayIcon={app}\OpenWinBox.exe
UninstallDisplayName=OpenWinBox
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
OutputDir=..\dist
OutputBaseFilename=OpenWinBox-{#AppVersion}-Setup
#ifdef SignedBuild
; Usa el desinstalador firmado de esta carpeta: sin firma, Smart App Control puede bloquear sus copias temporales
SignedUninstaller=yes
SignedUninstallerDir=..\dist\uninstaller
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\OpenWinBox"; Filename: "{app}\OpenWinBox.exe"
Name: "{autodesktop}\OpenWinBox"; Filename: "{app}\OpenWinBox.exe"; Tasks: desktopicon

[Registry]
; La app crea este valor si el usuario activa el autoinicio: no dejarlo apuntando a un exe borrado
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "OpenWinBox"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\OpenWinBox.exe"; Description: "{cm:LaunchProgram,OpenWinBox}"; Flags: nowait postinstall skipifsilent
; Sin asistente (actualización desde la app): reabrirla sin preguntar
Filename: "{app}\OpenWinBox.exe"; Flags: nowait; Check: WizardSilent
