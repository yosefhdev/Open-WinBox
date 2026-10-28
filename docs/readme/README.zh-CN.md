<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox 图标" />

# OpenWinBox

**开源的 Windows 桌面小组件：文件夹、音乐、时钟、天气和音频，一眼可见。**

![版本](https://img.shields.io/badge/版本-1.0.0--beta.1-orange)
![平台](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![许可证](https://img.shields.io/badge/许可证-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · **简体中文** · [日本語](README.ja.md) · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox 目前处于 **Beta** 阶段。不同版本之间可能会有变化或问题。请在 Issues 页面反馈。

## 功能

- **文件夹小组件。** 任意文件夹都能变成一个小组件，显示其中的文件和快捷方式。把文件或快捷方式拖到上面即可添加，双击打开。
- **像浏览器一样的标签页。** 把多个小组件合成一个：将一个小组件拖到另一个上，拖动标签页调整顺序，或把标签页拖出来重新分开。
- **随你喜好。** 重命名小组件，选择表情符号或图片作为图标，锁定位置，按任意顺序排列图标。
- **始终留在桌面。** 小组件位于其他应用之后，不会出现在任务栏和 Alt+Tab 中，并能与彼此及屏幕边缘轻柔对齐。
- **功能小组件：**
  - **音乐**：显示任意应用正在播放的内容，带控制按钮和三种样式。
  - **Spotify**：只显示 Spotify，支持随机播放和循环。
  - **Glance**：在 Bing 每日图片上显示大号时钟，可选第二时区，12 或 24 小时制。
  - **天气**：你所在位置或任意城市的天气。
  - **音频**：切换输出设备和麦克风，单独调节每个应用的音量（类似 Win+G）。
- **轻量。** 空闲时约占 0.005% 的 CPU。
- **从 DeskBox 导入。** 如果已安装 DeskBox 1.5，首次启动时会导入你的小组件、顺序和图标。

## 系统要求

- Windows 10 1809 或更高版本，或 Windows 11（64 位）。
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。从源码编译需要 .NET 8 SDK。

## 安装

### 方式一：下载发行版

1. 打开本仓库的 **Releases** 页面，下载最新的 `OpenWinBox-<版本>-win-x64.zip`。
2. 解压到一个长期保留的文件夹，例如 `C:\Apps\OpenWinBox`。
3. 运行 `OpenWinBox.exe`。首次运行会打开快速入门向导。

> 发行包将随第一个公开 Beta 版发布。

### 方式二：从源码编译

```bash
git clone <仓库地址>
cd <仓库文件夹>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

生成的程序位于 `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`。

## 使用方法

> 目前界面为**西班牙语**；下表中以*斜体*标出界面上的实际文字。

| 操作 | 方法 |
|---|---|
| 显示或隐藏所有小组件 | 按 **F7**，或单击托盘图标 |
| 打开设置 | 右键单击托盘图标 → *Configuración…*，或再次启动应用 |
| 新建小组件 | 任意小组件上的 **+** → *Nuevo widget*，或 *Configuración → Widgets de archivos → Nuevo widget* |
| 在小组件中新建标签页 | **+** → *Nueva pestaña en este widget* |
| 重命名、更换图标、锁定、分组 | 小组件上的 **⋯** → *Cambiar nombre*、*Cambiar icono…*、*Bloquear posición*、*Agrupar con…* |
| 添加文件 | 把文件或快捷方式拖到小组件上（按住 **Ctrl** 可复制快捷方式而不是移动） |
| 合并两个小组件 | 按住小组件的标题栏拖到另一个小组件的标题栏上停留，出现 *Suelta para unir* 后松开 |
| 调整或拆分标签页 | 左右拖动标签页调整顺序，拖出小组件即可成为独立小组件 |
| 移动功能小组件 | 鼠标悬停后拖动顶部的拖动条 |
| 开启或关闭功能小组件 | *Configuración → Widgets funcionales*（单击某一行查看选项） |
| 开机启动 | *Configuración → General → Lanzar al inicio*，或托盘菜单中的 *Iniciar con Windows* |

设置保存在 `%LOCALAPPDATA%\OpenWinBox`。删除小组件或标签页不会删除你的文件。

## 卸载

1. 在设置中关闭 *Lanzar al inicio*，然后在托盘菜单中选择 *Salir*。
2. 删除应用文件夹。
3. 可选：删除 `%LOCALAPPDATA%\OpenWinBox` 以清除设置。

## 版本

OpenWinBox 采用[语义化版本](https://semver.org/lang/zh-CN/)。正式版 `1.0.0` 之前的测试版编号为 `1.0.0-beta.N`。详见 [CHANGELOG.md](../../CHANGELOG.md)。

## 参与贡献

欢迎提交问题报告、建议和 Pull Request。较大的改动请先开一个 Issue 讨论。

## 许可证

[MIT](../../LICENSE) © 2026 Yosefh。第三方组件和在线服务见 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)。天气数据来自 [Open-Meteo.com](https://open-meteo.com/)（CC BY 4.0）。

OpenWinBox 是独立项目，与 Microsoft、Spotify 或 DeskBox 无关。
