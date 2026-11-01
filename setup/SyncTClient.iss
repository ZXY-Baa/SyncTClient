; Das Installationspaket fuer SyncTClient.
;
; Uebersetzt wird es von tools\Veroeffentlichen.ps1; die Fassung kommt von
; dort und steht nicht hier, sonst gaebe es zwei Stellen mit derselben Zahl:
;
;   ISCC.exe /DFassung=1.2.3 /DQuelle=...\BIN /DZiel=...\dist setup\SyncTClient.iss

#ifndef Fassung
  #define Fassung "0.0.0"
#endif

#ifndef Quelle
  #define Quelle "..\BIN"
#endif

#ifndef Ziel
  #define Ziel "..\dist"
#endif

[Setup]
; Eigene Kennung, unabhaengig vom Ursprungsprojekt: Windows erkennt daran eine
; bestehende Installation dieser Fortfuehrung und ersetzt sie. Eine Installation
; der Ursprungsquelle daneben bleibt eine eigene Anwendung.
AppId={{413AD422-5021-4A80-A5F3-18548D600903}
AppName=SyncTClient
AppVersion={#Fassung}
AppVerName=SyncTClient {#Fassung}
VersionInfoVersion={#Fassung}
AppPublisher=ZXY_Baa

; Ohne Administratorrechte -- aber die Wahl bleibt.
;
; Vorgabe ist die Installation fuer den angemeldeten Benutzer, nach
; %LOCALAPPDATA%\Programs. Das Programm traegt seine Shell-Erweiterung unter
; HKEY_CURRENT_USER ein und meldet seine Sync-Wurzeln fuer den angemeldeten
; Benutzer an; fuer alle Benutzer zu installieren verspricht mehr, als es
; einhalten kann -- ein zweiter Benutzer bekaeme ein Programm, das fuer ihn
; erst beim ersten Start etwas tut.
;
; Wer trotzdem nach C:\Program Files will, bekommt die Frage: mit "dialog"
; stellt Inno sie als erste Seite. Die Daten liegen ohnehin nicht beim
; Programm, sondern unter %LOCALAPPDATA%\SyncTClient, deshalb traegt beides.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\SyncTClient
DefaultGroupName=SyncTClient
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\SyncTClient.exe
UninstallDisplayName=SyncTClient {#Fassung}

; Nur auf 64-Bit-Windows. Die Erweiterung ist nativ fuer x64 gebaut, und der
; Platzhalter-Dienst von Windows gibt es unter 32 Bit ohnehin nicht.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041

OutputDir={#Ziel}
OutputBaseFilename=SyncTClient-{#Fassung}-setup
SetupIconFile=..\src\SyncTClient.Gui\SyncTClient.ico

; Das Programmsymbol im Assistenten: gross auf der ersten und letzten Seite,
; klein oben rechts auf allen dazwischen. Inno nimmt BMP, deshalb liegen sie
; als solche daneben; die Fassung mit @2x waehlt es bei hoher Punktdichte.
WizardImageFile=WizardImage.bmp,WizardImage@2x.bmp
WizardSmallImageFile=WizardSmallImage.bmp,WizardSmallImage@2x.bmp
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern



[Languages]
; Die Nutzungsbedingungen haengen an der Sprache. Inno zeigt sie als eigene
; Seite mit den Schaltern "Ich akzeptiere" / "Ich akzeptiere nicht"; ohne
; Zustimmung geht es nicht weiter. Der Haftungsausschluss ist der Grund fuer
; diese Seite -- in der Lizenz steht er auch, aber dort liest ihn niemand.
;
; English steht zuerst: die erste Sprache der Liste ist die Vorgabe fuer
; Systeme, deren Sprache nicht dabei ist.
;
; ChineseSimplified.isl liegt neben diesem Skript und wird nicht aus dem
; Inno-eigenen Languages-Verzeichnis genommen: die Uebersetzung gibt es dort
; erst seit Juni 2026, und welche Fassung auf einem Rechner liegt, soll das
; Ergebnis nicht bestimmen.
Name: "english"; MessagesFile: "compiler:Default.isl";           LicenseFile: "EULA.en.txt"
Name: "deutsch"; MessagesFile: "compiler:Languages\German.isl"; LicenseFile: "EULA.de.txt"
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"; LicenseFile: "EULA.zh-CN.txt"

[Messages]
english.WelcomeLabel2=This will install SyncTClient {#Fassung} on this computer.%n%nThe program registers its Explorer integration -- thumbnails and context menu -- itself on first start. No administrator rights are needed for that.
deutsch.WelcomeLabel2=Damit wird SyncTClient {#Fassung} auf diesem Rechner eingerichtet.%n%nDie Einbindung in den Explorer -- Vorschaubilder und Kontextmenü -- trägt das Programm beim ersten Start selbst ein. Administratorrechte werden dafür nicht gebraucht.
chinesesimplified.WelcomeLabel2=将在本计算机上安装 SyncTClient {#Fassung}。%n%n程序会在首次启动时自行注册资源管理器集成（缩略图和上下文菜单），不需要管理员权限。
[CustomMessages]
english.StartWithWindows=Start with Windows
deutsch.StartWithWindows=Beim Anmelden starten
chinesesimplified.StartWithWindows=随 Windows 启动

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked
Name: "autostart"; Description: "{cm:StartWithWindows}"; Flags: unchecked

[Files]
; Das ganze Veroeffentlichungsverzeichnis. Es ist self-contained -- die
; Laufzeit liegt darin, auf dem Zielrechner wird nichts vorausgesetzt.
Source: "{#Quelle}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; Lizenz und Bedingungen zum Nachlesen. Die Bedingungen nennen die Datei
; LICENSE.txt ausdruecklich; ohne sie waere der Verweis ins Leere.
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "EULA.de.txt"; DestDir: "{app}"; DestName: "Nutzungsbedingungen.txt"; Flags: ignoreversion
Source: "EULA.en.txt"; DestDir: "{app}"; DestName: "TermsOfUse.txt"; Flags: ignoreversion
Source: "EULA.zh-CN.txt"; DestDir: "{app}"; DestName: "TermsOfUse.zh-CN.txt"; Flags: ignoreversion

[Icons]
Name: "{group}\SyncTClient"; Filename: "{app}\SyncTClient.exe"
Name: "{group}\{cm:UninstallProgram,SyncTClient}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SyncTClient"; Filename: "{app}\SyncTClient.exe"; Tasks: desktopicon
Name: "{userstartup}\SyncTClient"; Filename: "{app}\SyncTClient.exe"; Tasks: autostart

[Run]
Filename: "{app}\SyncTClient.exe"; Description: "{cm:LaunchProgram,SyncTClient}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Was das Programm zur Laufzeit neben sich anlegt. Ohne diese Zeilen bleibt
; das Verzeichnis nach dem Entfernen stehen.
Type: filesandordirs; Name: "{app}\synct-home"

[Code]
procedure InitializeWizard;
begin
  { Inno legt den Seitentitel 16 Punkte weiter links an als die Unterzeile.
    Im modernen Stil faellt das als schiefe Kante auf, deshalb beginnen
    beide hier an derselben Stelle. }
  WizardForm.PageNameLabel.Left := WizardForm.PageDescriptionLabel.Left;
end;