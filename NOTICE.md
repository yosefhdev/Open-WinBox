# Notice

OpenWinBox
Copyright (C) 2026 yosefhdev

Based on DeskBox
Copyright (C) Tianyu Zhu (https://github.com/Tianyu199509/DeskBox)

This program is free software: you can redistribute it and/or modify it under
the terms of the GNU General Public License as published by the Free Software
Foundation, version 3. It is distributed WITHOUT ANY WARRANTY. See the
[LICENSE](LICENSE) file for the full text.

## Changes from DeskBox

OpenWinBox started from DeskBox 1.5.9; its full git history is kept in this
repository. Changes made by OpenWinBox, starting 2026-11-05:

- **Name and identity:** the app, executable, installer, package identity,
  data folder, registry key, startup entry, single-instance lock and
  notification activator use the name OpenWinBox and new IDs, so it can be
  installed next to DeskBox.
- **Icons:** the app icon and logos are OpenWinBox's own.
- **Online services:** updates come from this repository's GitHub releases;
  the in-app feedback form that posted to deskbox.fun now opens GitHub Issues.
- **Credits:** the "About me" dialog of the original author is kept and is now
  labelled "Original author".
- **No donation codes:** the WeChat/Alipay support QR codes and the
  "Support DeskBox" dialog were removed; the "About me" dialog credits Tianyu
  Zhu and links to the original repository instead.
- **Microsoft Store edition:** published as OpenWinBox (Store ID
  9P91207LL0RC); privacy policy in [PRIVACY.md](PRIVACY.md).
- **New: Audio widget** (`Controls/WidgetContents/AudioWidgetContent*`,
  `Platform/CoreAudio.cs`, `Services/AudioWidgetContentProvider.cs`).
- **New: second time zone in Glance** (`GlanceWidgetData.SecondTimeZoneId`,
  `GlanceWidgetViewModel.SecondClockText`, Glance settings section).

Projects, folders and C# namespaces keep their DeskBox names so that upstream
changes can still be merged.
