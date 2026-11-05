<div align="center">

<img src="docs/images/openwinbox-icon.png" width="96" alt="OpenWinBox icon" />

# OpenWinBox

**Open-source desktop widgets and organizer for Windows, based on [DeskBox](https://github.com/Tianyu199509/DeskBox).**

[![Download](https://img.shields.io/badge/Download-Windows%20installer-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![Version](https://img.shields.io/badge/version-1.0.0--beta.4-orange)
![Platform](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
[![License: GPL v3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)

</div>

> [!NOTE]
> OpenWinBox is in **beta**. Please report problems in the [Issues](https://github.com/yosefhdev/Open-WinBox/issues) tab.

## Credits

OpenWinBox is a fork of **[DeskBox](https://github.com/Tianyu199509/DeskBox)** by **Tianyu Zhu ([@Tianyu199509](https://github.com/Tianyu199509))**, released under the GNU GPL v3. Almost everything in this app is DeskBox's work: the file widgets, Glance, todos, quick capture, search, weather, music, the settings app, the installer and the update system.

OpenWinBox keeps that code, changes the name and identity so both apps can be installed side by side, and adds the features listed below. See [NOTICE.md](NOTICE.md) for the full list of changes. OpenWinBox is not affiliated with or endorsed by the DeskBox author.

## What OpenWinBox adds

| Feature | What it does |
|---|---|
| 🔊 **Audio widget** | Switch the output device and the microphone, set their volume, mute them, and set the volume of each app, like the Win+G audio panel. Turn it on in *Settings → Feature widgets*. |
| 🕒 **Second time zone in Glance** | Show the time in another city next to the date. Pick the zone in *Settings → Glance → Font → Second time zone*. |

Everything else comes from DeskBox. Its [README](https://github.com/Tianyu199509/DeskBox#readme) describes all the widgets in detail.

## Can I run it next to DeskBox?

Yes. OpenWinBox uses its own data folder (`%LOCALAPPDATA%\OpenWinBox`), registry key, startup entry, single-instance lock and package identity, so it does not touch an installed DeskBox.

## Privacy

OpenWinBox has no account and no analytics. Settings, files, notes and tasks stay on your PC.

These features connect from your PC to online services:

| Feature | Service |
|---|---|
| Updates | GitHub releases of this repository |
| Weather | Open-Meteo, MSN Weather, and an IP location service (ipapi.co, ip-api.com or ip.sb) when Windows location is off |
| Glance | Bing photo of the day and Wikimedia Commons |
| Feedback buttons | Open this repository's GitHub Issues in your browser |

The in-app feedback form inherited from DeskBox, which sent messages to the DeskBox author's server, is disabled.

## Building from source

You need the .NET SDK 10.0.402 or later (see `global.json`).

```powershell
dotnet build src/DeskBox/DeskBox.csproj
```

The Debug build runs from `src/DeskBox/bin/Debug/net10.0-windows10.0.22621.0/OpenWinBox.exe`. Projects, folders and namespaces keep their DeskBox names on purpose, so changes from upstream DeskBox can still be merged.

## License

[GNU General Public License v3.0](LICENSE), the same license as DeskBox. If you distribute a modified version, you must also publish its source code under the GPL v3.
