<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox-Symbol" />

# OpenWinBox

**Open-Source-Widgets für den Windows-Desktop: Ordner, Musik, Uhr, Wetter und Audio immer im Blick.**

[![Herunterladen](https://img.shields.io/badge/Herunterladen-Windows--Installationsprogramm-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Version](https://img.shields.io/badge/Version-1.0.0--beta.1-orange)
![Plattform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Lizenz](https://img.shields.io/badge/Lizenz-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · **Deutsch** · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="Illustration der OpenWinBox-Widgets auf einem Desktop: ein Ordner-Widget mit Tabs, eine Uhr, Wetter, Musik und Audiosteuerung" />

<sub>Illustration der Widget-Typen. Das tatsächliche Aussehen hängt von Design und Einstellungen ab.</sub>

</div>

> [!NOTE]
> OpenWinBox ist in der **Beta**-Phase. Zwischen Versionen kann sich manches ändern oder nicht funktionieren. Bitte melde Probleme im Tab „[Issues](https://github.com/yosefhdev/Open-WinBox/issues)“.

## Was ist OpenWinBox?

Der Windows-Desktop füllt sich schnell mit losen Dateien, und um die Uhrzeit im Ausland zu sehen oder den Titel zu wechseln, muss man eine weitere App öffnen. OpenWinBox legt kleine Widgets auf den Desktop, damit all das immer im Blick ist.

Es läuft im Infobereich der Taskleiste, braucht kein Konto, und das Entfernen eines Widgets löscht nie deine Dateien.

## Widgets

### Ordner-Widgets

Mach aus jedem Ordner ein Widget, das seine Dateien und Verknüpfungen anzeigt.

- **Ziehen und Ablegen:** Ziehe Dateien oder Verknüpfungen auf ein Widget, um sie hinzuzufügen, und öffne sie per Doppelklick.
- **Tabs wie im Browser:** Fasse mehrere Widgets zu einem zusammen, sortiere die Tabs per Ziehen um oder ziehe einen Tab heraus, um ihn wieder zu trennen.
- **Dein Stil:** Benenne Widgets um, verwende ein Emoji oder Bild als Symbol, sperre ihre Position und ordne die Symbole beliebig an.

### Funktions-Widgets

Schalte jedes einzeln ein oder aus und klicke in den Einstellungen auf seine Zeile, um die Optionen zu sehen.

| Widget | Was es macht |
|---|---|
| 🎵 **Musik** | Zeigt, was in einer beliebigen App läuft, mit Wiedergabesteuerung und drei Stilen |
| 🟢 **Spotify** | Dieselbe Idee nur für Spotify, mit Zufallswiedergabe und Wiederholung |
| 🕒 **Glance** | Große Uhr über dem Bing-Foto des Tages, mit optionaler zweiter Zeitzone und 12- oder 24-Stunden-Format |
| ☀️ **Wetter** | Das aktuelle Wetter für deinen Standort oder eine beliebige Stadt |
| 🔊 **Audio** | Wechsle Ausgabe und Mikrofon und stelle die Lautstärke jeder App ein, wie mit Win+G |

## Was dir gefallen könnte

- **Bleibt auf dem Desktop.** Widgets liegen hinter deinen Apps, bleiben mit Win+D sichtbar und erscheinen nie in der Taskleiste oder bei Alt+Tab.
- **Rastet sauber ein.** Widgets richten sich sanft aneinander und an den Bildschirmrändern aus.
- **Helles und dunkles Design.** Folge dem Systemdesign oder wähle eines aus.
- **Ruhig im Leerlauf.** Es braucht etwa 0,05 Sekunden CPU-Zeit pro Minute.
- **Import aus DeskBox.** Ist DeskBox 1.5 installiert, werden deine Widgets, ihre Reihenfolge und Symbole beim ersten Start übernommen.

## Installation

**Du brauchst** Windows 10 ab Version 1809 oder Windows 11 (64 Bit). Alles Weitere bringt das Installationsprogramm mit, .NET musst du also nicht installieren.

1. Lade die neueste `OpenWinBox-<Version>-Setup.exe` von der Seite [Releases](https://github.com/yosefhdev/Open-WinBox/releases) herunter.
2. Führe sie aus. Die Installation gilt nur für deinen Benutzer und braucht keine Administratorrechte.
3. Öffne OpenWinBox. Beim ersten Start öffnet sich eine kurze Einführung.

> [!IMPORTANT]
> Das Installationsprogramm ist noch nicht signiert, daher zeigt Windows eventuell *Der Computer wurde durch Windows geschützt* an. Wähle **Weitere Informationen** und dann **Trotzdem ausführen**. Zum Prüfen des Downloads kannst du den Hash mit `SHA256SUMS.txt` im Release vergleichen. Wenn die intelligente App-Steuerung aktiviert ist, kann Windows die App ohne Möglichkeit zum Fortfahren blockieren.

## Bedienung

> [!TIP]
> Die Oberfläche ist derzeit auf **Spanisch**. Die Beschriftungen, die du auf dem Bildschirm siehst, stehen hier *kursiv*.

Merke dir zwei Dinge: **F7** blendet alle Widgets ein oder aus, und das **Symbol im Infobereich** öffnet per Rechtsklick das Menü.

<details open>
<summary><b>Alltägliche Aktionen</b></summary>
<br>

| Aktion | So geht's |
|---|---|
| Alle Widgets ein- oder ausblenden | Drücke **F7** oder klicke mit links auf das Symbol im Infobereich |
| Einstellungen öffnen | Rechtsklick auf das Symbol im Infobereich → *Configuración…*, oder starte die App erneut |
| Neues Widget | **+** auf einem Widget → *Nuevo widget*, oder *Configuración → Widgets de archivos → Nuevo widget* |
| Neuer Tab in einem Widget | **+** → *Nueva pestaña en este widget* |
| Dateien hinzufügen | Ziehe Dateien oder Verknüpfungen auf das Widget (halte **Strg** gedrückt, um eine Verknüpfung zu kopieren statt zu verschieben) |

</details>

<details>
<summary><b>Widgets organisieren</b></summary>
<br>

| Aktion | So geht's |
|---|---|
| Umbenennen, Symbol ändern, sperren, gruppieren | **⋯** auf dem Widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Zwei Widgets gruppieren | Ziehe ein Widget an seiner Leiste und halte es über die Leiste eines anderen, bis *Suelta para unir* erscheint, dann lass los |
| Tabs umsortieren oder trennen | Ziehe einen Tab seitwärts zum Umsortieren oder aus dem Widget heraus, um ihn eigenständig zu machen |
| Funktions-Widget verschieben | Fahre mit der Maus darüber und ziehe am Griff oben |

</details>

<details>
<summary><b>Einstellungen</b></summary>
<br>

| Aktion | So geht's |
|---|---|
| Funktions-Widgets ein- oder ausschalten | *Configuración → Widgets funcionales* |
| Design ändern | *Configuración → Apariencia → Tema* |
| Mit Windows starten | *Configuración → General → Lanzar al inicio*, oder *Iniciar con Windows* im Menü des Infobereichs |

</details>

## Updates

Öffne *Configuración → Acerca de* und klicke auf *Buscar actualizaciones*. Gibt es eine neue Version, lädt *Actualizar* sie herunter, prüft ihren Hash und installiert sie, wobei deine Einstellungen erhalten bleiben. Die App schließt sich kurz und öffnet sich wieder.

Wähle unter *Canal de actualizaciones* die Option *Oficiales* für nur stabile Versionen oder *Betas*, um auch Testversionen zu erhalten. OpenWinBox verbindet sich dafür nur mit GitHub, wenn du auf die Schaltfläche klickst.

## Datenschutz

OpenWinBox hat kein Konto und keine eigene Nutzungsanalyse. Deine Einstellungen bleiben auf deinem PC, in `%LOCALAPPDATA%\OpenWinBox`.

Zwei Widgets verbinden sich direkt von deinem PC aus mit Onlinediensten, ohne API-Schlüssel:

| Widget | Dienst | Wofür |
|---|---|---|
| Wetter | [Open-Meteo](https://open-meteo.com/) | Vorhersage und Städtesuche |
| Wetter | [BigDataCloud](https://www.bigdatacloud.com/) | Stadtname für deinen Standort |
| Wetter | [ipapi.co](https://ipapi.co/) | Ungefährer Standort, wenn die Windows-Ortung aus ist |
| Glance | Microsoft Bing | Foto des Tages |

Die Widgets Musik und Spotify lesen, was Windows als aktuelle Wiedergabe meldet, daher ist keine Anmeldung nötig. Die Bedingungen der einzelnen Dienste stehen in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md).

## Deinstallieren

1. Wähle *Salir* im Menü des Infobereichs.
2. Deinstalliere OpenWinBox unter **Windows-Einstellungen → Apps → Installierte Apps**. Dabei wird auch der Autostart entfernt.
3. Optional: Lösche `%LOCALAPPDATA%\OpenWinBox`, um auch deine Einstellungen zu entfernen.

## Aus dem Quellcode kompilieren

Du brauchst das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

Die App wird unter `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe` erstellt.

Um das Installationsprogramm zu erzeugen, installiere [Inno Setup 6](https://jrsoftware.org/isinfo.php) und führe `tools/publish.ps1` aus. Das Ergebnis liegt in `dist/`.

Erstellt mit C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) und dem [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versionen

OpenWinBox verwendet [semantische Versionierung](https://semver.org/lang/de/). Betas heißen `1.0.0-beta.N` bis zur finalen `1.0.0`. Siehe [CHANGELOG.md](../../CHANGELOG.md).

## Mitwirken

Fehlerberichte, Ideen und Pull Requests sind willkommen. Für größere Änderungen bitte zuerst ein Issue eröffnen. Der Einstieg in den Code steht im [Entwicklerhandbuch](../development.md) (auf Englisch).

## Lizenz

[MIT](../../LICENSE) © 2026 Yosefh. Komponenten und Dienste von Drittanbietern stehen in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Wetterdaten von [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox ist ein unabhängiges Projekt und steht in keiner Verbindung zu Microsoft, Spotify oder DeskBox.
