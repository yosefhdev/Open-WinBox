<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox のアイコン" />

# OpenWinBox

**Windows デスクトップ用のオープンソースウィジェット。フォルダー、音楽、時計、天気、オーディオをいつでも目の前に。**

[![ダウンロード](https://img.shields.io/badge/%E3%83%80%E3%82%A6%E3%83%B3%E3%83%AD%E3%83%BC%E3%83%89-Windows%20%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%A9%E3%83%BC-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![バージョン](https://img.shields.io/badge/バージョン-1.0.0--beta.2-orange)
![プラットフォーム](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![ライセンス](https://img.shields.io/badge/ライセンス-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · **日本語** · [한국어](README.ko.md)

<br>

<img src="../banner.svg" width="100%" alt="デスクトップ上の OpenWinBox ウィジェットのイラスト：タブ付きフォルダーウィジェット、時計、天気、音楽、オーディオコントロール" />

<sub>ウィジェットの種類を示すイラストです。実際の見た目はテーマと設定によって異なります。</sub>

</div>

> [!NOTE]
> OpenWinBox は現在 **ベータ版** です。バージョン間で仕様が変わったり、不具合が起きたりすることがあります。問題は [Issues](https://github.com/yosefhdev/Open-WinBox/issues) タブで報告してください。

## OpenWinBox とは

Windows のデスクトップはファイルが散らかりがちで、海外の時刻を確認したり曲を変えたりするにも別のアプリを開く必要があります。OpenWinBox はデスクトップに小さなウィジェットを置き、これらをいつでもひと目で確認できるようにします。

システムトレイで動作し、アカウントは不要です。ウィジェットを削除してもファイルが消えることはありません。

## ウィジェット

### フォルダーウィジェット

任意のフォルダーを、中のファイルとショートカットを表示するウィジェットにできます。

- **ドラッグ＆ドロップ：** ファイルやショートカットをウィジェットにドラッグして追加し、ダブルクリックで開きます。
- **ブラウザーのようなタブ：** 複数のウィジェットを 1 つにまとめ、ドラッグでタブを並べ替えたり、タブを引き出して分離したりできます。
- **自分好みに：** ウィジェットの名前を変更し、絵文字や画像をアイコンに設定し、位置を固定して、アイコンを好きな順に並べられます。

### 機能ウィジェット

それぞれ個別にオン／オフでき、設定で行をクリックするとオプションが表示されます。

| ウィジェット | できること |
|---|---|
| 🎵 **音楽** | あらゆるアプリで再生中の内容を、再生コントロールと 3 つのスタイルで表示します |
| 🟢 **Spotify** | 同じ機能の Spotify 専用版で、シャッフルとリピートに対応します |
| 🕒 **Glance** | Bing の今日の写真に大きな時計を重ねて表示します。第 2 タイムゾーンと 12／24 時間表示を選べます |
| ☀️ **天気** | 現在地または任意の都市の現在の天気を表示します |
| 🔊 **オーディオ** | Win+G のように、出力とマイクの切り替えやアプリごとの音量調整ができます |

## おすすめのポイント

- **デスクトップにとどまる。** ウィジェットはアプリの背面にあり、Win+D でも表示されたままで、タスクバーや Alt+Tab には表示されません。
- **きれいに整列。** ウィジェット同士や画面の端にやさしく吸着します。
- **ライトテーマとダークテーマ。** システムのテーマに合わせることも、自分で選ぶこともできます。
- **アイドル時は静か。** CPU 使用時間は 1 分あたり約 0.05 秒です。
- **DeskBox から取り込み。** DeskBox 1.5 がインストールされていれば、初回起動時にウィジェット、並び順、アイコンを取り込みます。

## インストール

**動作環境：** Windows 10 バージョン 1809 以降、または Windows 11（64 ビット）。その他に必要なものはインストーラーに含まれているため、.NET のインストールは不要です。

1. [Releases](https://github.com/yosefhdev/Open-WinBox/releases) ページから最新の `OpenWinBox-<バージョン>-Setup.exe` をダウンロードします。
2. 実行します。現在のユーザーにのみインストールされ、管理者権限は必要ありません。
3. OpenWinBox を開きます。初回はクイックガイドが開きます。

> [!IMPORTANT]
> インストーラーはまだコード署名されていないため、「Windows によって PC が保護されました」と表示されることがあります。**詳細情報**、**実行** の順に選択してください。ダウンロードを確認するには、ハッシュをリリースの `SHA256SUMS.txt` と照合します。 スマート アプリ コントロールがオンの場合、Windows がアプリをブロックし、続行できないことがあります。

## 使い方

> [!TIP]
> 現在、画面の表示は**スペイン語**です。画面に表示される文言は*斜体*で記載しています。

覚えておく操作は 2 つです。**F7** ですべてのウィジェットを表示／非表示にし、**トレイアイコン**を右クリックするとメニューが開きます。

<details open>
<summary><b>よく使う操作</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| すべてのウィジェットを表示／非表示 | **F7** を押すか、トレイアイコンを左クリックします |
| 設定を開く | トレイアイコンを右クリック → *Configuración…*、またはアプリをもう一度起動します |
| 新しいウィジェット | ウィジェットの **+** → *Nuevo widget*、または *Configuración → Widgets de archivos → Nuevo widget* |
| ウィジェットに新しいタブを追加 | **+** → *Nueva pestaña en este widget* |
| ファイルを追加 | ファイルやショートカットをウィジェットにドラッグします（**Ctrl** を押しながらドラッグすると、ショートカットを移動せずコピーします） |

</details>

<details>
<summary><b>ウィジェットの整理</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| 名前変更、アイコン変更、固定、グループ化 | ウィジェットの **⋯** → *Cambiar nombre*、*Cambiar icono…*、*Bloquear posición*、*Agrupar con…* |
| 2 つのウィジェットをまとめる | ウィジェットをバーでドラッグし、別のウィジェットのバーの上で *Suelta para unir* が表示されるまで保持してから離します |
| タブの並べ替えや分離 | タブを左右にドラッグして並べ替えるか、ウィジェットの外へドラッグして独立させます |
| 機能ウィジェットを移動 | カーソルを合わせ、上部のハンドルをドラッグします |

</details>

<details>
<summary><b>設定</b></summary>
<br>

| 操作 | 方法 |
|---|---|
| 機能ウィジェットのオン／オフ | *Configuración → Widgets funcionales* |
| テーマの変更 | *Configuración → Apariencia → Tema* |
| Windows と同時に起動 | *Configuración → General → Lanzar al inicio*、またはトレイメニューの *Iniciar con Windows* |

</details>

## アップデート

*Configuración → Acerca de* を開き、*Buscar actualizaciones* を押します。新しいバージョンがあれば、*Actualizar* でダウンロードとハッシュの確認を行い、設定を保持したままインストールします。アプリは一度閉じてから再び開きます。

*Canal de actualizaciones* で、安定版のみを受け取るには *Oficiales* を、テスト版も受け取るには *Betas* を選択します。OpenWinBox がこのために GitHub に接続するのは、ボタンを押したときだけです。

## プライバシー

OpenWinBox にはアカウントも独自の分析機能もありません。設定はお使いの PC の `%LOCALAPPDATA%\OpenWinBox` に保存されます。

次の 2 つのウィジェットは、API キーなしで PC からオンラインサービスに直接接続します。

| ウィジェット | サービス | 用途 |
|---|---|---|
| 天気 | [Open-Meteo](https://open-meteo.com/) | 天気予報と都市の検索 |
| 天気 | [BigDataCloud](https://www.bigdatacloud.com/) | 現在地の都市名 |
| 天気 | [ipapi.co](https://ipapi.co/) | Windows の位置情報がオフのときのおおよその位置 |
| Glance | Microsoft Bing | 今日の写真 |

音楽ウィジェットと Spotify ウィジェットは Windows が報告する再生情報を読み取るため、サインインは不要です。各サービスの利用条件は [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) をご覧ください。

## アンインストール

1. トレイメニューで *Salir* を選択します。
2. **Windows の設定 → アプリ → インストールされているアプリ** から OpenWinBox をアンインストールします。Windows と同時に起動する設定も削除されます。
3. 任意：`%LOCALAPPDATA%\OpenWinBox` を削除すると、設定も消去されます。

## ソースからビルド

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) が必要です。

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

アプリは `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe` に作成されます。

インストーラーを作成するには、[Inno Setup 6](https://jrsoftware.org/isinfo.php) をインストールして `tools/publish.ps1` を実行します。結果は `dist/` に出力されます。

C#、[WinUI 3](https://github.com/microsoft/microsoft-ui-xaml)、[Windows App SDK](https://github.com/microsoft/WindowsAppSDK) で作られています。

## バージョン

OpenWinBox は[セマンティックバージョニング](https://semver.org/lang/ja/)を採用しています。正式版 `1.0.0` までのベータ版は `1.0.0-beta.N` と番号付けされます。[CHANGELOG.md](../../CHANGELOG.md) を参照してください。

## コントリビュート

バグ報告、アイデア、プルリクエストを歓迎します。大きな変更は、先に Issue を作成してください。コードを使い始めるには、[開発者ガイド](../development.md)（英語）をご覧ください。

## ライセンス

[MIT](../../LICENSE) © 2026 Yosefh。サードパーティのコンポーネントとオンラインサービスは [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) に記載しています。天気データ提供：[Open-Meteo.com](https://open-meteo.com/)（CC BY 4.0）。

OpenWinBox は独立したプロジェクトであり、Microsoft、Spotify、DeskBox とは関係ありません。
