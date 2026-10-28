<div align="center">

<img src="docs/icon.png" width="96" alt="OpenWinBox icon" />

# OpenWinBox

**Open-source desktop widgets for Windows: your folders, music, clock, weather and audio, right on the desktop.**

![Version](https://img.shields.io/badge/version-1.0.0--beta.1-orange)
![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![License](https://img.shields.io/badge/license-MIT-green)

**English** · [Español](docs/readme/README.es.md) · [Français](docs/readme/README.fr.md) · [Português](docs/readme/README.pt-BR.md) · [Deutsch](docs/readme/README.de.md) · [Italiano](docs/readme/README.it.md) · [Русский](docs/readme/README.ru.md) · [简体中文](docs/readme/README.zh-CN.md) · [日本語](docs/readme/README.ja.md) · [한국어](docs/readme/README.ko.md)

</div>

> [!NOTE]
> OpenWinBox is in **beta**. Things may change or break between versions. Please report problems in the Issues tab.

## Features

- **Folder widgets.** Any folder becomes a widget with its files and shortcuts. Drag files or shortcuts onto it to add them; double-click to open.
- **Browser-like tabs.** Group several widgets into one: drag a widget onto another, reorder tabs by dragging, or drag a tab out to split it again.
- **Your style.** Rename widgets, pick an emoji or image as the icon, lock their position, and arrange icons in any order.
- **Stays on the desktop.** Widgets sit behind your apps, never show in the taskbar or Alt+Tab, and snap softly to each other and to screen edges.
- **Functional widgets:**
  - **Music**: what's playing in any app, with controls and three styles.
  - **Spotify**: Spotify only, with shuffle and repeat.
  - **Glance**: big clock over the Bing photo of the day, optional second time zone, 12 h or 24 h.
  - **Weather**: current weather for your location or any city.
  - **Audio**: switch output and microphone, and set the volume of each app (like Win+G).
- **Light on resources.** About 0.005 % CPU when idle.
- **Imports from DeskBox.** If DeskBox 1.5 is installed, your widgets, order and icons are imported on first launch.

## Requirements

- Windows 10 version 1809 or later, or Windows 11 (64-bit).
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64). To build from source you need the .NET 8 SDK instead.

## Installation

### Option 1: download a release

1. Open the **Releases** page of this repository and download the latest `OpenWinBox-<version>-win-x64.zip`.
2. Extract it to a folder you will keep, for example `C:\Apps\OpenWinBox`.
3. Run `OpenWinBox.exe`. A quick setup guide opens the first time.

> Release packages will be published with the first public beta.

### Option 2: build from source

```bash
git clone <repository-url>
cd <repository-folder>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

The app is created in `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

## How to use

> The interface is currently in **Spanish**; the exact labels you will see are shown in *italics*.

| Action | How |
|---|---|
| Show or hide all widgets | Press **F7**, or left-click the tray icon |
| Open Settings | Right-click the tray icon → *Configuración…*, or launch the app again |
| New widget | **+** on any widget → *Nuevo widget*, or *Configuración → Widgets de archivos → Nuevo widget* |
| New tab in a widget | **+** → *Nueva pestaña en este widget* |
| Rename, change icon, lock, group | **⋯** on the widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Add files | Drag files or shortcuts onto the widget (hold **Ctrl** to copy a shortcut instead of moving it) |
| Group two widgets | Drag a widget by its bar and hold it over another widget's bar until *Suelta para unir* appears, then release |
| Reorder or split tabs | Drag a tab sideways to reorder it, or out of the widget to make it independent |
| Move a functional widget | Hover it and drag the handle at the top |
| Turn functional widgets on or off | *Configuración → Widgets funcionales* (click a row for its options) |
| Start with Windows | *Configuración → General → Lanzar al inicio*, or *Iniciar con Windows* in the tray menu |

Settings are saved in `%LOCALAPPDATA%\OpenWinBox`. Removing a widget or tab never deletes your files.

## Uninstall

1. Turn off *Lanzar al inicio* in Settings, then choose *Salir* in the tray menu.
2. Delete the app folder.
3. Optional: delete `%LOCALAPPDATA%\OpenWinBox` to remove your settings.

## Versions

OpenWinBox uses [Semantic Versioning](https://semver.org/). Betas are numbered `1.0.0-beta.N` until the final `1.0.0`. See [CHANGELOG.md](CHANGELOG.md).

## Contributing

Bug reports, ideas and pull requests are welcome. Please open an issue first for large changes.

## License

[MIT](LICENSE) © 2026 Yosefh. Third-party components and online services are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Weather data by [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox is an independent project and is not affiliated with Microsoft, Spotify or DeskBox.
