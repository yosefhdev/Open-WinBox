<div align="center">

<img src="docs/icon.png" width="96" alt="OpenWinBox icon" />

# OpenWinBox

**Open-source desktop widgets for Windows.**<br>
Your folders, music, clock, weather and audio controls, right on the desktop.

[![Download](https://img.shields.io/badge/Download-Windows%20installer-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Version](https://img.shields.io/badge/version-1.0.0--beta.2-orange)
![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![License](https://img.shields.io/badge/license-MIT-green)

**English** · [Español](docs/readme/README.es.md) · [Français](docs/readme/README.fr.md) · [Português](docs/readme/README.pt-BR.md) · [Deutsch](docs/readme/README.de.md) · [Italiano](docs/readme/README.it.md) · [Русский](docs/readme/README.ru.md) · [简体中文](docs/readme/README.zh-CN.md) · [日本語](docs/readme/README.ja.md) · [한국어](docs/readme/README.ko.md)

<br>

<img src="docs/banner.svg" width="100%" alt="Illustration of OpenWinBox widgets on a desktop: a folder widget with tabs, a clock, weather, music and audio controls" />

<sub>Illustration of the widget types. The real look depends on your theme and settings.</sub>

</div>

> [!NOTE]
> OpenWinBox is in **beta**, so things may change or break between versions. Please report problems in the [Issues](https://github.com/yosefhdev/Open-WinBox/issues) tab.

## What is OpenWinBox?

The Windows desktop tends to fill up with loose files, and checking the time abroad or changing a song means opening another app. OpenWinBox puts small widgets on the desktop so those things are always one glance away.

It runs in the system tray, needs no account, and removing a widget never deletes your files.

## Widgets

### Folder widgets

Turn any folder into a widget that shows its files and shortcuts.

- **Drag and drop:** drag files or shortcuts onto a widget to add them, and double-click to open them.
- **Browser-like tabs:** group several widgets into one, reorder the tabs by dragging, or pull a tab out to split it again.
- **Your style:** rename widgets, use an emoji or image as the icon, lock their position and arrange the icons in any order.

### Functional widgets

Turn each one on or off separately, and click its row in Settings to see its options.

| Widget | What it does |
|---|---|
| 🎵 **Music** | Shows what's playing in any app, with playback controls and three styles |
| 🟢 **Spotify** | The same idea for Spotify only, with shuffle and repeat |
| 🕒 **Glance** | Big clock over the Bing photo of the day, with an optional second time zone and 12 or 24 hour format |
| ☀️ **Weather** | Current weather for your location or any city |
| 🔊 **Audio** | Switch output and microphone, and set the volume of each app, like Win+G |

## Why you might like it

- **Stays on the desktop.** Widgets sit behind your apps, stay visible with Win+D, and never appear in the taskbar or Alt+Tab.
- **Snaps into place.** Widgets align softly with each other and with the screen edges.
- **Light and dark themes.** Follow the system theme or pick one.
- **Quiet when idle.** It uses about 0.05 seconds of CPU per minute.
- **Imports from DeskBox.** If DeskBox 1.5 is installed, your widgets, order and icons are imported on first launch.

## Installation

**You need** Windows 10 version 1809 or later, or Windows 11 (64-bit). The installer includes everything else, so you don't have to install .NET.

1. Download the latest `OpenWinBox-<version>-Setup.exe` from the [Releases](https://github.com/yosefhdev/Open-WinBox/releases) page.
2. Run it. It installs for your user only and doesn't ask for administrator rights.
3. Open OpenWinBox. A quick setup guide opens the first time.

> [!IMPORTANT]
> The installer isn't code-signed yet. OpenWinBox is applying for free code signing from the [SignPath Foundation](https://signpath.org/) ([policy](docs/code-signing-policy.md)). Until then, Windows may show *Windows protected your PC*. Choose **More info** and then **Run anyway**. To check your download, compare its hash with `SHA256SUMS.txt` in the release. If Smart App Control is on, Windows may block the app with no option to continue.

## How to use

> [!TIP]
> The interface is currently in **Spanish**. The labels you will see on screen are shown in *italics*.

The two controls to remember are **F7**, which shows or hides all widgets, and the **tray icon**, which opens the menu with a right-click.

<details open>
<summary><b>Everyday actions</b></summary>
<br>

| Action | How |
|---|---|
| Show or hide all widgets | Press **F7**, or left-click the tray icon |
| Open Settings | Right-click the tray icon → *Configuración…*, or launch the app again |
| New widget | **+** on any widget → *Nuevo widget*, or *Configuración → Widgets de archivos → Nuevo widget* |
| New tab in a widget | **+** → *Nueva pestaña en este widget* |
| Add files | Drag files or shortcuts onto the widget (hold **Ctrl** to copy a shortcut instead of moving it) |

</details>

<details>
<summary><b>Organizing widgets</b></summary>
<br>

| Action | How |
|---|---|
| Rename, change icon, lock, group | **⋯** on the widget → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| Group two widgets | Drag a widget by its bar and hold it over another widget's bar until *Suelta para unir* appears, then release |
| Reorder or split tabs | Drag a tab sideways to reorder it, or out of the widget to make it independent |
| Move a functional widget | Hover it and drag the handle at the top |

</details>

<details>
<summary><b>Settings</b></summary>
<br>

| Action | How |
|---|---|
| Turn functional widgets on or off | *Configuración → Widgets funcionales* |
| Change the theme | *Configuración → Apariencia → Tema* |
| Start with Windows | *Configuración → General → Lanzar al inicio*, or *Iniciar con Windows* in the tray menu |

</details>

## Updates

Open *Configuración → Acerca de* and press *Buscar actualizaciones*. If there's a new version, *Actualizar* downloads it, checks its hash and installs it, keeping your settings. The app closes for a moment and opens again.

In *Canal de actualizaciones*, choose *Oficiales* for stable releases only, or *Betas* to also get test versions. OpenWinBox only connects to GitHub for this when you press the button.

## Privacy

OpenWinBox has no account and no analytics of its own. Your settings stay on your PC in `%LOCALAPPDATA%\OpenWinBox`.

Two widgets connect directly from your PC to online services, without an API key:

| Widget | Service | Used for |
|---|---|---|
| Weather | [Open-Meteo](https://open-meteo.com/) | Forecast and city search |
| Weather | [BigDataCloud](https://www.bigdatacloud.com/) | City name for your device location |
| Weather | [ipapi.co](https://ipapi.co/) | Approximate location when Windows location is off |
| Glance | Microsoft Bing | Photo of the day |

The Music and Spotify widgets read what Windows reports as playing, so they don't need you to sign in. Terms for each service are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Uninstall

1. Choose *Salir* in the tray menu.
2. Uninstall OpenWinBox from **Windows Settings → Apps → Installed apps**. This also removes its start-with-Windows entry.
3. Optional: delete `%LOCALAPPDATA%\OpenWinBox` to remove your settings too.

## Build from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

The app is created at `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

To generate the installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run `tools/publish.ps1`. The result goes in `dist/`.

Built with C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) and the [Windows App SDK](https://github.com/microsoft/WindowsAppSDK).

## Versions

OpenWinBox uses [Semantic Versioning](https://semver.org/). Betas are numbered `1.0.0-beta.N` until the final `1.0.0`. See the [CHANGELOG](CHANGELOG.md).

## Contributing

Bug reports, ideas and pull requests are welcome. Please open an issue first for large changes. To get started with the code, see the [developer guide](docs/development.md).

## Code signing

See the [code signing policy](docs/code-signing-policy.md).

## License

[MIT](LICENSE) © 2026 Yosefh. Third-party components and online services are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Weather data by [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox is an independent project and is not affiliated with Microsoft, Spotify or DeskBox.
