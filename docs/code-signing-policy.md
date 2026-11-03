# Code signing policy

**Status:** OpenWinBox is applying for free code signing from the [SignPath Foundation](https://signpath.org/). Current releases are not signed yet.

Once the project is approved, this page will state: Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

## What gets signed

Only files built from this repository, by the [Build installer](../.github/workflows/release.yml) workflow on GitHub-hosted runners:

- `OpenWinBox.exe` and `OpenWinBox.dll`
- The uninstaller created by Inno Setup
- `OpenWinBox-<version>-Setup.exe`

Third-party files are not signed with this certificate. They are listed in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Team

| Role | Members |
|---|---|
| Committers and reviewers | [yosefhdev](https://github.com/yosefhdev) |
| Approvers | [yosefhdev](https://github.com/yosefhdev) |

Every signing request is approved by hand. Team members use multi-factor authentication on GitHub and SignPath.

## Privacy

OpenWinBox has no account and no analytics of its own. It connects to other systems only in these cases, and each one can be avoided:

- **Weather and Glance widgets:** they connect to the services listed in the [README](../README.md#privacy) only while they are turned on. Both are off on a new install.
- **Updates:** the app connects to GitHub only when you press *Buscar actualizaciones*.

Your settings stay on your PC in `%LOCALAPPDATA%\OpenWinBox`.
