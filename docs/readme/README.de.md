<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox-Symbol" />

# OpenWinBox

**Open-Source-Widgets für den Windows-Desktop: Ordner, Musik, Uhr, Wetter und Audio immer im Blick.**

![Version](https://img.shields.io/badge/Version-1.0.0--beta.2-orange)
![Plattform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![Lizenz](https://img.shields.io/badge/Lizenz-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · **Deutsch** · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox ist in der **Beta**-Phase. Zwischen Versionen kann sich manches ändern oder nicht funktionieren. Bitte melde Probleme im Tab „Issues“.

## Funktionen

- **Ordner-Widgets.** Jeder Ordner wird zu einem Widget mit seinen Dateien und Verknüpfungen. Ziehe Dateien oder Verknüpfungen darauf, um sie hinzuzufügen; Doppelklick zum Öffnen.
- **Tabs wie im Browser.** Fasse mehrere Widgets zu einem zusammen: Ziehe ein Widget auf ein anderes, ordne Tabs per Ziehen neu oder ziehe einen Tab heraus, um ihn wieder abzutrennen.
- **Nach deinem Geschmack.** Widgets umbenennen, ein Emoji oder Bild als Symbol wählen, die Position sperren und Symbole beliebig anordnen.
- **Bleibt auf dem Desktop.** Widgets liegen hinter deinen Apps, erscheinen weder in der Taskleiste noch in Alt+Tab und richten sich sanft aneinander und an den Bildschirmrändern aus.
- **Funktions-Widgets:**
  - **Musik**: was in einer beliebigen App läuft, mit Steuerung und drei Stilen.
  - **Spotify**: nur Spotify, mit Zufallswiedergabe und Wiederholen.
  - **Glance**: große Uhr über dem Bing-Bild des Tages, optional zweite Zeitzone, 12 oder 24 Stunden.
  - **Wetter**: das Wetter an deinem Standort oder in jeder Stadt.
  - **Audio**: Ausgabe und Mikrofon wechseln und die Lautstärke jeder App einstellen (wie Win+G).
- **Ressourcenschonend.** Etwa 0,005 % CPU im Leerlauf.
- **Import aus DeskBox.** Ist DeskBox 1.5 installiert, werden beim ersten Start deine Widgets, ihre Reihenfolge und Symbole übernommen.

## Voraussetzungen

- Windows 10 ab Version 1809 oder Windows 11 (64 Bit).
- Alles Weitere bringt das Installationsprogramm mit. Zum Kompilieren aus dem Quellcode wird das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) benötigt.

## Installation

### Option 1: Version herunterladen

1. Öffne die Seite **Releases** dieses Repositorys und lade die neueste `OpenWinBox-<Version>-Setup.exe` herunter.
2. Führe sie aus. Die Installation gilt nur für deinen Benutzer und braucht keine Administratorrechte.
3. Öffne OpenWinBox. Beim ersten Start öffnet sich eine kurze Einführung.

> Das Installationsprogramm ist noch nicht signiert, daher zeigt Windows eventuell *Der Computer wurde durch Windows geschützt* an. Wähle **Weitere Informationen** und dann **Trotzdem ausführen**. Den Hash kannst du mit `SHA256SUMS.txt` im Release vergleichen.

### Option 2: aus dem Quellcode kompilieren

```bash
git clone <repository-url>
cd <repository-ordner>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

Die App liegt dann unter `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## Bedienung

> Die Oberfläche ist derzeit auf **Spanisch**; die genauen Beschriftungen stehen *kursiv*.

| Aktion | So geht's |
|---|---|
| Alle Widgets ein- oder ausblenden | **F7** drücken oder auf das Infobereich-Symbol klicken |
| Einstellungen öffnen | Rechtsklick auf das Infobereich-Symbol → *Configuración…*, oder die App erneut starten |
| Neues Widget | **+** auf einem Widget → *Nuevo widget*, oder *Configuración → Widgets de archivos → Nuevo widget* |
| Neuer Tab in einem Widget | **+** → *Nueva pestaña en este widget* |
| Umbenennen, Symbol ändern, sperren, gruppieren | **⋯** am Widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Dateien hinzufügen | Dateien oder Verknüpfungen auf das Widget ziehen (**Strg** gedrückt halten, um eine Verknüpfung zu kopieren statt zu verschieben) |
| Zwei Widgets gruppieren | Ein Widget an seiner Leiste ziehen und über der Leiste eines anderen halten, bis *Suelta para unir* erscheint, dann loslassen |
| Tabs neu ordnen oder abtrennen | Tab seitlich ziehen zum Umsortieren oder aus dem Widget heraus, um ihn eigenständig zu machen |
| Funktions-Widget verschieben | Mit der Maus darüberfahren und den Griff oben ziehen |
| Funktions-Widgets ein- oder ausschalten | *Configuración → Widgets funcionales* (Zeile anklicken für Optionen) |
| Mit Windows starten | *Configuración → General → Lanzar al inicio*, oder *Iniciar con Windows* im Infobereich-Menü |

Die Einstellungen liegen in `%LOCALAPPDATA%\OpenWinBox`. Das Entfernen eines Widgets oder Tabs löscht nie deine Dateien.

## Deinstallation

1. *Lanzar al inicio* in den Einstellungen ausschalten und im Infobereich-Menü *Salir* wählen.
2. Den App-Ordner löschen.
3. Optional: `%LOCALAPPDATA%\OpenWinBox` löschen, um die Einstellungen zu entfernen.

## Versionen

OpenWinBox verwendet [semantische Versionierung](https://semver.org/lang/de/). Betas heißen `1.0.0-beta.N` bis zur finalen `1.0.0`. Siehe [CHANGELOG.md](../../CHANGELOG.md).

## Mitwirken

Fehlerberichte, Ideen und Pull Requests sind willkommen. Für größere Änderungen bitte zuerst ein Issue eröffnen.

## Lizenz

[MIT](../../LICENSE) © 2026 Yosefh. Komponenten und Dienste von Drittanbietern stehen in [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md). Wetterdaten von [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox ist ein unabhängiges Projekt und steht in keiner Verbindung zu Microsoft, Spotify oder DeskBox.
