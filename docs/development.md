# Developer guide

How to build OpenWinBox, find your way around the code and add a widget.

Code comments and the app's interface are in Spanish. Issues and pull requests in English or Spanish are both fine.

## Requirements

- Windows 10 version 1809 or later, or Windows 11 (64-bit)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Only to build the installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`)

## Build and run

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

Run `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`.

Two things can get in your way:

- **Only one copy runs at a time.** If OpenWinBox is already running, for example an installed copy, launching your build only opens Settings in the running copy and then exits. Choose *Salir* in the tray menu first.
- **Smart App Control.** With it on, Windows can refuse to run unsigned builds.

Your build uses the same data as an installed copy: `%LOCALAPPDATA%\OpenWinBox\config.json`. Back it up before testing changes to the config.

## Project structure

```
src/OpenWinBox/
├── App.xaml(.cs)        Startup, single instance, opening widgets and windows
├── Assets/              App icon (must stay here: it is loaded from the output folder)
├── Core/
│   ├── WidgetConfig.cs  Saved settings and each widget's options
│   └── Updater.cs       Update search, download and hash check
├── Shell/               Tray icon, tray menu, F7 hotkey, start with Windows
├── Settings/            Settings window, its rows, first-run guide
└── Widgets/
    ├── Host/            The window every widget lives in: dragging, snapping, tabs, backdrop
    ├── Files/           Helpers for folder widgets: opening files, icon picker
    ├── Music/           Music and Spotify (same view)
    ├── Glance/          Clock over the Bing photo of the day
    ├── Weather/
    └── Audio/
tools/
├── publish.ps1          Builds the installer and SHA256SUMS.txt
├── installer.iss        Inno Setup script
├── UpdaterCheck/        Tests for the update logic
└── make_icon.py         Generates Assets/OpenWinBox.ico
```

Everything uses the `OpenWinBox` namespace, whatever its folder.

## How it fits together

1. `App.OnLaunched` takes the single-instance mutex, loads `AppConfig`, and opens one `WidgetWindow` for each enabled widget.
2. Each `WidgetConfig` has a `Kind`. `"File"` is a folder widget, and any other value is a functional widget.
3. `WidgetWindow.SetupFeature` puts the matching view inside the window for functional widgets. The window handles moving, resizing and staying on the desktop, so a view only draws its content.
4. Folder widgets have no view of their own: their tabs, file list, and drag and drop are in `Widgets/Host/WidgetWindow.xaml.cs`.
5. `AppConfig.Save` writes the settings to `config.json`. Every functional widget keeps a record, and `IsDisabled` turns it off without losing its position.

## Add a functional widget

For a widget called `Notes`:

1. Create `Widgets/Notes/NotesView.xaml` and `NotesView.xaml.cs`, a `UserControl` in the `OpenWinBox` namespace. The project includes new files automatically.
2. In `Core/WidgetConfig.cs`, add `"Notes"` to `AppConfig.FeatureKinds`. Add its size to `DefaultSize` if the default doesn't suit it.
3. In `Widgets/Host/WidgetWindow.xaml.cs`, add it to `FeatureTitle` and add `"Notes" => new NotesView()` to `SetupFeature`. Without this it falls through to `GlanceView`.
4. In `Settings/SettingsWindow.xaml`, copy another widget's row under *Widgets funcionales*, with a `ToggleSwitch` named `NotesToggle` and `Tag="Notes"`. Then add its line to `RefreshFeatures` in `SettingsWindow.xaml.cs`.
5. If it has options, add an options class in `WidgetConfig.cs`, a page in `SettingsWindow.xaml`, and an entry in `_featurePages`. Views can listen to `App.FeatureOptionsChanged` to apply changes right away.

If it connects to an online service, list that service in the README's Privacy section and in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Tests

```bash
cd tools/UpdaterCheck
dotnet run
```

This checks version ordering, channel selection and hash parsing, and prints `OK`. There are no automated UI tests, so try your change in the running app, with both light and dark themes if it changes the interface.

## Build the installer

Run `tools/publish.ps1` in PowerShell. It creates these files in `dist/`:

- `OpenWinBox-<version>-Setup.exe`
- `SHA256SUMS.txt`

Don't change `AppId` in `tools/installer.iss`, because that is how a new version replaces the old one. The mutex name in `AppMutex` must match the one in `App.xaml.cs`.

## Publish a release

Versions follow [Semantic Versioning](https://semver.org/): `1.0.0-beta.N` until the final `1.0.0`.

1. Set `<Version>` in `src/OpenWinBox/OpenWinBox.csproj`.
2. Add the changes to [CHANGELOG.md](../CHANGELOG.md) and update the version badge in the READMEs.
3. Run `tools/publish.ps1`.
4. Commit, then create the release. Leave out `--prerelease` for a final version.

```bash
gh release create v1.0.0-beta.1 dist/OpenWinBox-1.0.0-beta.1-Setup.exe dist/SHA256SUMS.txt --prerelease
```

The in-app updater skips a release unless its tag is `v<version>` and it has both files with exactly these names. It offers prereleases only on the *Betas* channel.

The installer isn't code-signed yet. `SHA256SUMS.txt` detects a damaged download, but it doesn't protect against someone who controls the GitHub account.
