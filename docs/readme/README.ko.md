<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox 아이콘" />

# OpenWinBox

**Windows 바탕 화면을 위한 오픈 소스 위젯: 폴더, 음악, 시계, 날씨, 오디오를 한눈에.**

[![다운로드](https://img.shields.io/badge/%EB%8B%A4%EC%9A%B4%EB%A1%9C%EB%93%9C-Windows%20%EC%84%A4%EC%B9%98%20%ED%94%84%EB%A1%9C%EA%B7%B8%EB%9E%A8-E9705A?style=for-the-badge)](https://github.com/yosefhdev/Open-WinBox/releases)

![버전](https://img.shields.io/badge/버전-1.0.0--beta.2-orange)
![플랫폼](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![라이선스](https://img.shields.io/badge/라이선스-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · **한국어**

<br>

<img src="../banner.svg" width="100%" alt="바탕 화면의 OpenWinBox 위젯 그림: 탭이 있는 폴더 위젯, 시계, 날씨, 음악, 오디오 컨트롤" />

<sub>위젯 종류를 보여 주는 그림입니다. 실제 모습은 테마와 설정에 따라 다릅니다.</sub>

</div>

> [!NOTE]
> OpenWinBox는 현재 **베타** 단계입니다. 버전에 따라 기능이 바뀌거나 문제가 생길 수 있습니다. 문제는 [Issues](https://github.com/yosefhdev/Open-WinBox/issues) 탭에 알려 주세요.

## OpenWinBox란?

Windows 바탕 화면은 흩어진 파일로 금방 가득 차고, 다른 나라의 시간을 보거나 노래를 바꾸려면 앱을 하나 더 열어야 합니다. OpenWinBox는 바탕 화면에 작은 위젯을 두어 이런 정보를 언제나 한눈에 볼 수 있게 합니다.

시스템 트레이에서 실행되고 계정이 필요 없으며, 위젯을 제거해도 파일은 절대 삭제되지 않습니다.

## 위젯

### 폴더 위젯

어떤 폴더든 파일과 바로 가기를 보여 주는 위젯으로 만들 수 있습니다.

- **끌어서 놓기:** 파일이나 바로 가기를 위젯으로 끌어 추가하고, 두 번 클릭하여 엽니다.
- **브라우저 같은 탭:** 여러 위젯을 하나로 묶고, 탭을 끌어 순서를 바꾸거나 밖으로 끌어내 다시 분리합니다.
- **나만의 스타일:** 위젯 이름을 바꾸고, 이모지나 이미지를 아이콘으로 쓰고, 위치를 잠그고, 아이콘을 원하는 순서로 정렬합니다.

### 기능 위젯

각각 따로 켜고 끌 수 있으며, 설정에서 해당 행을 클릭하면 옵션이 표시됩니다.

| 위젯 | 기능 |
|---|---|
| 🎵 **음악** | 모든 앱에서 재생 중인 항목을 재생 컨트롤 및 세 가지 스타일과 함께 표시합니다 |
| 🟢 **Spotify** | 같은 기능의 Spotify 전용 위젯으로, 셔플과 반복을 지원합니다 |
| 🕒 **Glance** | Bing 오늘의 사진 위에 큰 시계를 표시하며, 두 번째 시간대와 12/24시간 형식을 선택할 수 있습니다 |
| ☀️ **날씨** | 현재 위치나 원하는 도시의 현재 날씨를 표시합니다 |
| 🔊 **오디오** | Win+G처럼 출력 장치와 마이크를 전환하고 앱별 볼륨을 조절합니다 |

## 이런 점이 좋습니다

- **바탕 화면에 머뭅니다.** 위젯은 앱 뒤에 있고, Win+D를 눌러도 계속 보이며, 작업 표시줄과 Alt+Tab에는 나타나지 않습니다.
- **알아서 정렬됩니다.** 위젯은 서로 그리고 화면 가장자리에 부드럽게 맞춰집니다.
- **밝은 테마와 어두운 테마.** 시스템 테마를 따르거나 직접 고를 수 있습니다.
- **유휴 시 조용합니다.** 1분에 약 0.05초의 CPU 시간을 사용합니다.
- **DeskBox에서 가져오기.** DeskBox 1.5가 설치되어 있으면 처음 실행할 때 위젯, 순서, 아이콘을 가져옵니다.

## 설치

**필요 환경:** Windows 10 버전 1809 이상 또는 Windows 11(64비트). 나머지는 모두 설치 프로그램에 포함되어 있어 .NET을 설치할 필요가 없습니다.

1. [Releases](https://github.com/yosefhdev/Open-WinBox/releases) 페이지에서 최신 `OpenWinBox-<버전>-Setup.exe`를 다운로드합니다.
2. 실행합니다. 현재 사용자에게만 설치되며 관리자 권한이 필요하지 않습니다.
3. OpenWinBox를 엽니다. 처음 실행하면 빠른 안내가 열립니다.

> [!IMPORTANT]
> 설치 프로그램은 아직 코드 서명이 되어 있지 않아 *Windows의 PC 보호* 창이 나타날 수 있습니다. **추가 정보**를 누른 다음 **실행**을 선택하세요. 다운로드를 확인하려면 해시를 릴리스의 `SHA256SUMS.txt`와 비교하세요. 스마트 앱 컨트롤이 켜져 있으면 Windows가 앱을 차단하고 계속할 수 없을 수 있습니다.

## 사용 방법

> [!TIP]
> 현재 인터페이스는 **스페인어**입니다. 화면에 표시되는 문구는 *기울임꼴*로 적었습니다.

기억할 조작은 두 가지입니다. **F7**은 모든 위젯을 표시하거나 숨기고, **트레이 아이콘**을 마우스 오른쪽 버튼으로 클릭하면 메뉴가 열립니다.

<details open>
<summary><b>자주 쓰는 작업</b></summary>
<br>

| 작업 | 방법 |
|---|---|
| 모든 위젯 표시 또는 숨기기 | **F7**을 누르거나 트레이 아이콘을 왼쪽 클릭합니다 |
| 설정 열기 | 트레이 아이콘을 오른쪽 클릭 → *Configuración…*, 또는 앱을 다시 실행합니다 |
| 새 위젯 | 위젯의 **+** → *Nuevo widget*, 또는 *Configuración → Widgets de archivos → Nuevo widget* |
| 위젯에 새 탭 추가 | **+** → *Nueva pestaña en este widget* |
| 파일 추가 | 파일이나 바로 가기를 위젯으로 끕니다(**Ctrl**을 누르면 바로 가기를 이동하지 않고 복사합니다) |

</details>

<details>
<summary><b>위젯 정리</b></summary>
<br>

| 작업 | 방법 |
|---|---|
| 이름 변경, 아이콘 변경, 잠금, 그룹화 | 위젯의 **⋯** → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| 위젯 두 개 묶기 | 위젯의 막대를 끌어 다른 위젯의 막대 위에서 *Suelta para unir*가 나타날 때까지 기다린 다음 놓습니다 |
| 탭 순서 변경 또는 분리 | 탭을 옆으로 끌어 순서를 바꾸거나, 위젯 밖으로 끌어 독립시킵니다 |
| 기능 위젯 이동 | 마우스를 올린 다음 위쪽 핸들을 끕니다 |

</details>

<details>
<summary><b>설정</b></summary>
<br>

| 작업 | 방법 |
|---|---|
| 기능 위젯 켜기 또는 끄기 | *Configuración → Widgets funcionales* |
| 테마 변경 | *Configuración → Apariencia → Tema* |
| Windows와 함께 시작 | *Configuración → General → Lanzar al inicio*, 또는 트레이 메뉴의 *Iniciar con Windows* |

</details>

## 업데이트

*Configuración → Acerca de*를 열고 *Buscar actualizaciones*를 누릅니다. 새 버전이 있으면 *Actualizar*가 다운로드하고 해시를 확인한 뒤 설정을 유지한 채 설치합니다. 앱이 잠시 닫혔다가 다시 열립니다.

*Canal de actualizaciones*에서 안정 버전만 받으려면 *Oficiales*를, 테스트 버전도 받으려면 *Betas*를 선택합니다. OpenWinBox는 버튼을 누를 때만 이를 위해 GitHub에 연결합니다.

## 개인정보 보호

OpenWinBox에는 계정과 자체 분석 기능이 없습니다. 설정은 PC의 `%LOCALAPPDATA%\OpenWinBox`에 저장됩니다.

다음 두 위젯은 API 키 없이 PC에서 온라인 서비스에 직접 연결합니다.

| 위젯 | 서비스 | 용도 |
|---|---|---|
| 날씨 | [Open-Meteo](https://open-meteo.com/) | 일기 예보와 도시 검색 |
| 날씨 | [BigDataCloud](https://www.bigdatacloud.com/) | 현재 위치의 도시 이름 |
| 날씨 | [ipapi.co](https://ipapi.co/) | Windows 위치 서비스가 꺼져 있을 때의 대략적인 위치 |
| Glance | Microsoft Bing | 오늘의 사진 |

음악 및 Spotify 위젯은 Windows가 알려 주는 재생 정보를 읽으므로 로그인이 필요 없습니다. 각 서비스의 약관은 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)에 있습니다.

## 제거

1. 트레이 메뉴에서 *Salir*를 선택합니다.
2. **Windows 설정 → 앱 → 설치된 앱**에서 OpenWinBox를 제거합니다. Windows와 함께 시작하는 항목도 함께 제거됩니다.
3. 선택 사항: 설정까지 지우려면 `%LOCALAPPDATA%\OpenWinBox`를 삭제합니다.

## 소스에서 빌드

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)가 필요합니다.

```bash
git clone https://github.com/yosefhdev/Open-WinBox.git
cd Open-WinBox
dotnet build src/OpenWinBox/OpenWinBox.csproj -c Release
```

앱은 `src/OpenWinBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`에 생성됩니다.

설치 프로그램을 만들려면 [Inno Setup 6](https://jrsoftware.org/isinfo.php)을 설치하고 `tools/publish.ps1`을 실행합니다. 결과물은 `dist/`에 생성됩니다.

C#, [WinUI 3](https://github.com/microsoft/microsoft-ui-xaml), [Windows App SDK](https://github.com/microsoft/WindowsAppSDK)로 만들었습니다.

## 버전

OpenWinBox는 [유의적 버전](https://semver.org/lang/ko/)을 사용합니다. 정식 `1.0.0` 전까지 베타는 `1.0.0-beta.N`으로 번호를 매깁니다. [CHANGELOG.md](../../CHANGELOG.md)를 참고하세요.

## 기여하기

버그 신고, 아이디어, 풀 리퀘스트를 환영합니다. 큰 변경은 먼저 이슈를 열어 주세요. 코드를 시작하려면 [개발자 가이드](../development.md)(영어)를 참고하세요.

## 라이선스

[MIT](../../LICENSE) © 2026 Yosefh. 서드파티 구성 요소와 온라인 서비스는 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)에 있습니다. 날씨 데이터 제공: [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox는 독립 프로젝트이며 Microsoft, Spotify, DeskBox와 관련이 없습니다.
