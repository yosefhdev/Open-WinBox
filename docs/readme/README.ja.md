<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox のアイコン" />

# OpenWinBox

**Windows デスクトップ用のオープンソースウィジェット。フォルダー、音楽、時計、天気、オーディオをいつでも目の前に。**

![バージョン](https://img.shields.io/badge/バージョン-1.0.0--beta.1-orange)
![プラットフォーム](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![ライセンス](https://img.shields.io/badge/ライセンス-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · **日本語** · [한국어](README.ko.md)

</div>

> [!NOTE]
> OpenWinBox は現在 **ベータ版** です。バージョン間で仕様が変わったり、不具合が起きたりすることがあります。問題は Issues タブで報告してください。

## 機能

- **フォルダーウィジェット。** どのフォルダーも、中のファイルやショートカットを表示するウィジェットになります。ファイルやショートカットをドラッグして追加し、ダブルクリックで開きます。
- **ブラウザーのようなタブ。** 複数のウィジェットを 1 つにまとめられます。ウィジェットを別のウィジェットにドラッグする、タブをドラッグして並べ替える、タブを外にドラッグして分離する、といった操作ができます。
- **好みに合わせて。** 名前の変更、絵文字や画像のアイコン設定、位置のロック、アイコンの自由な並べ替えができます。
- **常にデスクトップに。** ウィジェットはアプリの背面にとどまり、タスクバーや Alt+Tab に表示されず、ほかのウィジェットや画面の端にやさしく揃います。
- **機能ウィジェット：**
  - **ミュージック**：どのアプリで再生中の曲も表示。操作ボタンと 3 つのスタイル付き。
  - **Spotify**：Spotify 専用。シャッフルとリピートに対応。
  - **Glance**：Bing の今日の写真に大きな時計を表示。2 つ目のタイムゾーンも表示可能、12 時間制／24 時間制。
  - **天気**：現在地または任意の都市の天気。
  - **オーディオ**：出力デバイスとマイクの切り替え、アプリごとの音量調整（Win+G のように）。
- **軽量。** アイドル時の CPU 使用率は約 0.005%。
- **DeskBox からインポート。** DeskBox 1.5 がインストールされていれば、初回起動時にウィジェット、並び順、アイコンを取り込みます。

## 動作環境

- Windows 10 バージョン 1809 以降、または Windows 11（64 ビット）。
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。ソースからビルドする場合は .NET 8 SDK が必要です。

## インストール

### 方法 1：リリース版をダウンロード

1. このリポジトリの **Releases** ページを開き、最新の `OpenWinBox-<バージョン>-win-x64.zip` をダウンロードします。
2. 残しておくフォルダーに展開します（例：`C:\Apps\OpenWinBox`）。
3. `OpenWinBox.exe` を実行します。初回はクイックガイドが開きます。

> パッケージは最初の公開ベータ版と同時に公開予定です。

### 方法 2：ソースからビルド

```bash
git clone <リポジトリの URL>
cd <リポジトリのフォルダー>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

アプリは `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe` に作成されます。

## 使い方

> 現在、画面表示は **スペイン語** です。実際に表示される文字を *斜体* で示しています。

| 操作 | 方法 |
|---|---|
| すべてのウィジェットを表示／非表示 | **F7** を押すか、トレイアイコンをクリック |
| 設定を開く | トレイアイコンを右クリック → *Configuración…*、またはアプリをもう一度起動 |
| 新しいウィジェット | ウィジェットの **+** → *Nuevo widget*、または *Configuración → Widgets de archivos → Nuevo widget* |
| ウィジェットに新しいタブ | **+** → *Nueva pestaña en este widget* |
| 名前変更、アイコン変更、ロック、グループ化 | ウィジェットの **⋯** → *Cambiar nombre*、*Cambiar icono…*、*Bloquear posición*、*Agrupar con…* |
| ファイルを追加 | ファイルやショートカットをウィジェットにドラッグ（**Ctrl** を押しながらだとショートカットを移動せずにコピー） |
| 2 つのウィジェットをまとめる | ウィジェットをバーでドラッグし、別のウィジェットのバーの上で *Suelta para unir* が表示されるまで待ってから離す |
| タブの並べ替え・分離 | タブを横にドラッグして並べ替え、ウィジェットの外にドラッグすると独立したウィジェットになる |
| 機能ウィジェットを移動 | マウスを乗せて、上部のハンドルをドラッグ |
| 機能ウィジェットのオン／オフ | *Configuración → Widgets funcionales*（行をクリックするとオプション） |
| Windows と同時に起動 | *Configuración → General → Lanzar al inicio*、またはトレイメニューの *Iniciar con Windows* |

設定は `%LOCALAPPDATA%\OpenWinBox` に保存されます。ウィジェットやタブを削除しても、ファイルが削除されることはありません。

## アンインストール

1. 設定で *Lanzar al inicio* をオフにし、トレイメニューから *Salir* を選びます。
2. アプリのフォルダーを削除します。
3. 任意：設定も消す場合は `%LOCALAPPDATA%\OpenWinBox` を削除します。

## バージョン

OpenWinBox は[セマンティックバージョニング](https://semver.org/lang/ja/)を採用しています。正式版 `1.0.0` までのベータ版は `1.0.0-beta.N` と番号付けされます。[CHANGELOG.md](../../CHANGELOG.md) を参照してください。

## コントリビュート

バグ報告、アイデア、プルリクエストを歓迎します。大きな変更は、先に Issue を作成してください。

## ライセンス

[MIT](../../LICENSE) © 2026 Yosefh。サードパーティのコンポーネントとオンラインサービスは [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) に記載しています。天気データ提供：[Open-Meteo.com](https://open-meteo.com/)（CC BY 4.0）。

OpenWinBox は独立したプロジェクトであり、Microsoft、Spotify、DeskBox とは関係ありません。
