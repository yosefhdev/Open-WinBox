<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox 图标" />

# OpenWinBox

**开源的 Windows 桌面小组件：文件夹、音乐、时钟、天气和音频，一眼可见。**

[![下载](https://img.shields.io/badge/%E4%B8%8B%E8%BD%BD-Windows%20%E5%AE%89%E8%A3%85%E7%A8%8B%E5%BA%8F-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![版本](https://img.shields.io/badge/版本-1.0.0--beta.1-orange)
![平台](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![许可证](https://img.shields.io/badge/许可证-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · **简体中文** · [日本語](README.ja.md) · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="OpenWinBox 桌面小组件示意图：带标签页的文件夹小组件、时钟、天气、音乐和音频控制" />

<sub>小组件类型示意图。实际外观取决于你的主题和设置。</sub>

</div>

> [!NOTE]
> OpenWinBox 目前处于 **Beta** 阶段。不同版本之间可能会有变化或问题。请在 [Issues](https://github.com/yosefhdev/Open-WinBox/issues) 页面反馈。

## OpenWinBox 是什么？

Windows 桌面很容易堆满零散文件，而查看其他地区的时间或切歌又得再打开一个应用。OpenWinBox 在桌面上放置小组件，让这些内容随时一目了然。

它在系统托盘中运行，无需账户，移除小组件也绝不会删除你的文件。

## 小组件

### 文件夹小组件

把任意文件夹变成显示其文件和快捷方式的小组件。

- **拖放：** 将文件或快捷方式拖到小组件上即可添加，双击即可打开。
- **浏览器式标签页：** 将多个小组件合并为一个，拖动标签页调整顺序，或把标签页拖出来重新分开。
- **你的风格：** 重命名小组件，用表情符号或图片作为图标，锁定位置，并按任意顺序排列图标。

### 功能小组件

每个小组件都可以单独开关，在设置中点击对应的行即可查看选项。

| 小组件 | 功能 |
|---|---|
| 🎵 **音乐** | 显示任意应用正在播放的内容，带播放控制和三种样式 |
| 🟢 **Spotify** | 同样的功能，仅用于 Spotify，支持随机播放和重复播放 |
| 🕒 **Glance** | 在必应每日图片上显示大时钟，可选第二时区，支持 12 或 24 小时制 |
| ☀️ **天气** | 你所在位置或任意城市的当前天气 |
| 🔊 **音频** | 切换输出设备和麦克风，并调节每个应用的音量，类似 Win+G |

## 你可能会喜欢它的原因

- **始终留在桌面。** 小组件位于应用后方，按 Win+D 后依然可见，也不会出现在任务栏和 Alt+Tab 中。
- **自动吸附。** 小组件会平滑地彼此对齐，并对齐屏幕边缘。
- **浅色和深色主题。** 可跟随系统主题，也可自行选择。
- **空闲时几乎不占资源。** 每分钟约使用 0.05 秒 CPU 时间。
- **可从 DeskBox 导入。** 如果已安装 DeskBox 1.5，首次启动时会导入你的小组件、顺序和图标。

## 安装

**系统要求：** Windows 10 1809 或更高版本，或 Windows 11（64 位）。其余所需内容均已包含在安装程序中，无需安装 .NET。

1. 从 [Releases](https://github.com/yosefhdev/Open-WinBox/releases) 页面下载最新的 `OpenWinBox-<版本>-Setup.exe`。
2. 运行它。它只为当前用户安装，不需要管理员权限。
3. 打开 OpenWinBox。首次运行会打开快速入门向导。

> [!IMPORTANT]
> 安装程序尚未进行代码签名，因此 Windows 可能会显示“Windows 已保护你的电脑”。请选择 **更多信息**，然后选择 **仍要运行**。如需校验下载文件，可将其哈希值与发行版中的 `SHA256SUMS.txt` 对比。 如果开启了智能应用控制，Windows 可能会阻止此应用，且无法继续。

## 使用方法

> [!TIP]
> 目前界面为**西班牙语**。屏幕上显示的文字在下文中以*斜体*标出。

只需记住两个操作：**F7** 显示或隐藏所有小组件，右键单击**托盘图标**打开菜单。

<details open>
<summary><b>日常操作</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| 显示或隐藏所有小组件 | 按 **F7**，或左键单击托盘图标 |
| 打开设置 | 右键单击托盘图标 → *Configuración…*，或再次启动应用 |
| 新建小组件 | 在任意小组件上点击 **+** → *Nuevo widget*，或 *Configuración → Widgets de archivos → Nuevo widget* |
| 在小组件中新建标签页 | **+** → *Nueva pestaña en este widget* |
| 添加文件 | 将文件或快捷方式拖到小组件上（按住 **Ctrl** 可复制快捷方式而不是移动） |

</details>

<details>
<summary><b>整理小组件</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| 重命名、更换图标、锁定、分组 | 小组件上的 **⋯** → *Cambiar nombre*、*Cambiar icono…*、*Bloquear posición*、*Agrupar con…* |
| 合并两个小组件 | 拖动小组件的标题栏，停在另一个小组件的标题栏上，直到出现 *Suelta para unir*，然后松开 |
| 调整或分离标签页 | 左右拖动标签页可调整顺序，拖出小组件可使其独立 |
| 移动功能小组件 | 将鼠标悬停在其上，然后拖动顶部的手柄 |

</details>

<details>
<summary><b>设置</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| 开关功能小组件 | *Configuración → Widgets funcionales* |
| 更改主题 | *Configuración → Apariencia → Tema* |
| 随 Windows 启动 | *Configuración → General → Lanzar al inicio*，或托盘菜单中的 *Iniciar con Windows* |

</details>

## 更新

打开 *Configuración → Acerca de* 并点击 *Buscar actualizaciones*。如果有新版本，*Actualizar* 会下载并校验哈希值后安装，同时保留你的设置。应用会短暂关闭，然后重新打开。

在 *Canal de actualizaciones* 中，选择 *Oficiales* 只接收稳定版本，或选择 *Betas* 同时接收测试版本。只有在你点击按钮时，OpenWinBox 才会为此连接 GitHub。

## 隐私

OpenWinBox 没有账户，也没有自己的分析统计。你的设置保存在本机的 `%LOCALAPPDATA%\OpenWinBox` 中。

有两个小组件会从你的电脑直接连接在线服务，无需 API 密钥：

| 小组件 | 服务 | 用途 |
|---|---|---|
| 天气 | [Open-Meteo](https://open-meteo.com/) | 天气预报和城市搜索 |
| 天气 | [BigDataCloud](https://www.bigdatacloud.com/) | 你所在位置的城市名称 |
| 天气 | [ipapi.co](https://ipapi.co/) | Windows 定位关闭时的大致位置 |
| Glance | Microsoft Bing | 每日图片 |

音乐和 Spotify 小组件读取 Windows 报告的正在播放内容，因此无需登录。各服务的条款见 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)。

## 卸载

1. 在托盘菜单中选择 *Salir*。
2. 在 **Windows 设置 → 应用 → 已安装的应用** 中卸载 OpenWinBox。这也会移除其开机启动项。
3. 可选：删除 `%LOCALAPPDATA%\OpenWinBox` 以同时清除你的设置。

## 从源码编译

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)。

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

应用生成于 `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`。

要生成安装程序，请安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php) 并运行 `tools/publish.ps1`。结果位于 `dist/`。

使用 C#、[WinUI 3](https://github.com/microsoft/microsoft-ui-xaml) 和 [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) 构建。

## 版本

OpenWinBox 采用[语义化版本](https://semver.org/lang/zh-CN/)。正式版 `1.0.0` 之前的测试版编号为 `1.0.0-beta.N`。详见 [CHANGELOG.md](../../CHANGELOG.md)。

## 参与贡献

欢迎提交问题报告、建议和 Pull Request。较大的改动请先开一个 Issue 讨论。要开始了解代码，请参阅[开发者指南](../development.md)（英文）。

## 许可证

[MIT](../../LICENSE) © 2026 Yosefh。第三方组件和在线服务见 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)。天气数据来自 [Open-Meteo.com](https://open-meteo.com/)（CC BY 4.0）。

OpenWinBox 是独立项目，与 Microsoft、Spotify 或 DeskBox 无关。
