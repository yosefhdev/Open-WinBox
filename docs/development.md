# Developer guide

How to build OpenWinBox, find your way around the code and add a widget.

Code comments are in Spanish. The interface is in Spanish and English, see [Translate the interface](#translate-the-interface). Issues and pull requests in English or Spanish are both fine.

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

- **Only one copy runs at a time.** If OpenWinBox is already running, for example an installed copy, launching your build only opens Settings in the running copy and then exits. Choose *Exit* in the tray menu first.
- **Smart App Control.** With it on, Windows can refuse to run unsigned builds.

Your build uses the same data as an installed copy: `%LOCALAPPDATA%\OpenWinBox\config.json`. Back it up before testing changes to the config.

## Project structure

```
src/OpenWinBox/
├── App.xaml(.cs)        Startup, single instance, opening widgets and windows
├── Assets/              App icon (must stay here: it is loaded from the output folder)
├── Core/
│   ├── WidgetConfig.cs  Saved settings and each widget's options
│   ├── Updater.cs       Update search, download and hash check
│   ├── Lang.cs          Chooses the language and looks up texts
│   └── TExtension.cs    {local:T} for texts in XAML
├── Strings/             One JSON file per language, embedded in the .exe
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
├── UpdaterCheck/        Tests for the update logic and the translations
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
4. In `Settings/SettingsWindow.xaml`, copy another widget's row under *Functional widgets*, with a `ToggleSwitch` named `NotesToggle` and `Tag="Notes"`. Then add its line to `RefreshFeatures` in `SettingsWindow.xaml.cs`.
5. If it has options, add an options class in `WidgetConfig.cs`, a page in `SettingsWindow.xaml`, and an entry in `_featurePages`. Views can listen to `App.FeatureOptionsChanged` to apply changes right away.

Put every visible text in `Strings/` as described in [Translate the interface](#translate-the-interface).

If it connects to an online service, list that service in the README's Privacy section, in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md), and in `tools/privacy-en.txt` and `tools/privacy-es.txt`, which the installer shows.

## Translate the interface

All visible text is in `src/OpenWinBox/Strings/<code>.json`, as flat `"key": "text"` pairs. In code, use `Lang.T("key")`, or `Lang.T("key", value)` for texts with `{0}`. In XAML, use `Text="{local:T Key=key}"`. Don't use `x:Uid`.

To add a language, for example German:

1. Copy `Strings/en.json` to `Strings/de.json` and translate the values. Keep the keys and the `{0}` placeholders. The project embeds the file automatically.
2. Add `("de", "Deutsch")` to `Lang.Available` in `Core/Lang.cs`. It then appears in *Settings → General → Language*.
3. Run the [tests](#tests). They fail if a key or placeholder is missing.
4. Try it in the app: check the tray menu, the Weather widget and the Settings navigation pane, which have the least room for text.

How it behaves:

- **Applied right away.** Changing the language saves it as `Language` in `config.json` and recreates the open windows and widgets, because texts are read when a window is created.
- **Windows language.** `"System"`, also used by older config files, follows the Windows display language. If there is no translation for it, the interface uses English. A key missing from a translation also falls back to English.
- **Dates.** Glance and the other formats use `Lang.Culture`: the Windows regional format when it matches the chosen language, otherwise the language's own format.
- **Limits.** Text that belongs to Windows or WinUI stays in the Windows language: the folder picker, time zone names, and the context menu of text boxes.

`.resw` files with `x:Uid` aren't used because they are resolved for the language of the process when a page loads, so switching needs a restart, and a JSON file adds no extra DLLs to sign.

## Tests

```bash
cd tools/UpdaterCheck
dotnet run
```

This checks version ordering, channel selection, hash parsing, that every translation has the same keys and placeholders, and that every key used in the code exists. It prints `OK`. There are no automated UI tests, so try your change in the running app, with both light and dark themes if it changes the interface.

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

The installer isn't code-signed yet. `SHA256SUMS.txt` detects a damaged download, but it doesn't protect against someone who controls the GitHub account. See the [code signing policy](code-signing-policy.md).

The [Build installer](../.github/workflows/release.yml) workflow also builds the installer from a `v*` tag or by hand. It signs with SignPath when the `SIGNPATH_ORGANIZATION_ID` variable and `SIGNPATH_API_TOKEN` secret exist. The signing settings are in `.signpath/`.
