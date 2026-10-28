<div align="center">

<img src="../icon.png" width="96" alt="OpenWinBox 아이콘" />

# OpenWinBox

**Windows 바탕 화면을 위한 오픈 소스 위젯: 폴더, 음악, 시계, 날씨, 오디오를 한눈에.**

![버전](https://img.shields.io/badge/버전-1.0.0--beta.1-orange)
![플랫폼](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)
![라이선스](https://img.shields.io/badge/라이선스-MIT-green)

[English](../../README.md) · [Español](README.es.md) · [Français](README.fr.md) · [Português](README.pt-BR.md) · [Deutsch](README.de.md) · [Italiano](README.it.md) · [Русский](README.ru.md) · [简体中文](README.zh-CN.md) · [日本語](README.ja.md) · **한국어**

</div>

> [!NOTE]
> OpenWinBox는 현재 **베타** 단계입니다. 버전에 따라 기능이 바뀌거나 문제가 생길 수 있습니다. 문제는 Issues 탭에 알려 주세요.

## 기능

- **폴더 위젯.** 어떤 폴더든 파일과 바로 가기를 보여 주는 위젯이 됩니다. 파일이나 바로 가기를 끌어다 놓으면 추가되고, 두 번 클릭하면 열립니다.
- **브라우저 같은 탭.** 여러 위젯을 하나로 묶을 수 있습니다. 위젯을 다른 위젯 위로 끌어다 놓고, 탭을 끌어 순서를 바꾸고, 탭을 밖으로 끌어내 다시 분리합니다.
- **내 마음대로.** 위젯 이름 변경, 이모지나 이미지 아이콘 지정, 위치 잠금, 아이콘 자유 배치가 가능합니다.
- **항상 바탕 화면에.** 위젯은 다른 앱 뒤에 머물고, 작업 표시줄과 Alt+Tab에 나타나지 않으며, 서로 또는 화면 가장자리에 부드럽게 맞춰집니다.
- **기능 위젯:**
  - **음악**: 어떤 앱에서든 재생 중인 곡을 보여 주며, 컨트롤과 세 가지 스타일을 제공합니다.
  - **Spotify**: Spotify 전용, 셔플과 반복 지원.
  - **Glance**: Bing 오늘의 사진 위에 큰 시계, 선택적 두 번째 시간대, 12시간제 또는 24시간제.
  - **날씨**: 현재 위치나 원하는 도시의 날씨.
  - **오디오**: 출력 장치와 마이크 전환, 앱별 볼륨 조절(Win+G처럼).
- **가벼움.** 유휴 시 CPU 사용률 약 0.005%.
- **DeskBox에서 가져오기.** DeskBox 1.5가 설치되어 있으면 처음 실행할 때 위젯, 순서, 아이콘을 가져옵니다.

## 요구 사항

- Windows 10 버전 1809 이상 또는 Windows 11(64비트).
- [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)(x64). 소스에서 빌드하려면 .NET 8 SDK가 필요합니다.

## 설치

### 방법 1: 릴리스 다운로드

1. 이 저장소의 **Releases** 페이지에서 최신 `OpenWinBox-<버전>-win-x64.zip`을 다운로드합니다.
2. 계속 보관할 폴더에 압축을 풉니다(예: `C:\Apps\OpenWinBox`).
3. `OpenWinBox.exe`를 실행합니다. 처음 실행하면 빠른 안내가 열립니다.

> 패키지는 첫 공개 베타와 함께 게시될 예정입니다.

### 방법 2: 소스에서 빌드

```bash
git clone <저장소-URL>
cd <저장소-폴더>
dotnet build DeskBox/OpenWinBox.csproj -c Release
```

앱은 `DeskBox/bin/Release/net8.0-windows10.0.19041.0/win-x64/OpenWinBox.exe`에 만들어집니다.

## 사용 방법

> 현재 인터페이스는 **스페인어**입니다. 화면에 실제로 보이는 문구는 *기울임꼴*로 표시했습니다.

| 작업 | 방법 |
|---|---|
| 모든 위젯 표시/숨기기 | **F7**을 누르거나 트레이 아이콘을 클릭 |
| 설정 열기 | 트레이 아이콘 오른쪽 클릭 → *Configuración…*, 또는 앱을 다시 실행 |
| 새 위젯 | 위젯의 **+** → *Nuevo widget*, 또는 *Configuración → Widgets de archivos → Nuevo widget* |
| 위젯에 새 탭 | **+** → *Nueva pestaña en este widget* |
| 이름 변경, 아이콘 변경, 잠금, 그룹화 | 위젯의 **⋯** → *Cambiar nombre*, *Cambiar icono…*, *Bloquear posición*, *Agrupar con…* |
| 파일 추가 | 파일이나 바로 가기를 위젯으로 끌어다 놓기(**Ctrl**을 누르고 있으면 바로 가기를 이동하지 않고 복사) |
| 두 위젯 합치기 | 위젯의 막대를 잡고 다른 위젯의 막대 위에서 *Suelta para unir*가 나타날 때까지 기다린 뒤 놓기 |
| 탭 순서 변경/분리 | 탭을 옆으로 끌어 순서를 바꾸거나, 위젯 밖으로 끌어 독립된 위젯으로 만들기 |
| 기능 위젯 이동 | 마우스를 올리고 위쪽 손잡이를 끌기 |
| 기능 위젯 켜기/끄기 | *Configuración → Widgets funcionales*(행을 클릭하면 옵션) |
| Windows 시작 시 실행 | *Configuración → General → Lanzar al inicio*, 또는 트레이 메뉴의 *Iniciar con Windows* |

설정은 `%LOCALAPPDATA%\OpenWinBox`에 저장됩니다. 위젯이나 탭을 제거해도 파일은 절대 삭제되지 않습니다.

## 제거

1. 설정에서 *Lanzar al inicio*를 끄고 트레이 메뉴에서 *Salir*를 선택합니다.
2. 앱 폴더를 삭제합니다.
3. 선택 사항: 설정까지 지우려면 `%LOCALAPPDATA%\OpenWinBox`를 삭제합니다.

## 버전

OpenWinBox는 [유의적 버전](https://semver.org/lang/ko/)을 사용합니다. 정식 `1.0.0` 전까지 베타는 `1.0.0-beta.N`으로 번호를 매깁니다. [CHANGELOG.md](../../CHANGELOG.md)를 참고하세요.

## 기여하기

버그 신고, 아이디어, 풀 리퀘스트를 환영합니다. 큰 변경은 먼저 이슈를 열어 주세요.

## 라이선스

[MIT](../../LICENSE) © 2026 Yosefh. 서드파티 구성 요소와 온라인 서비스는 [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md)에 있습니다. 날씨 데이터 제공: [Open-Meteo.com](https://open-meteo.com/) (CC BY 4.0).

OpenWinBox는 독립 프로젝트이며 Microsoft, Spotify, DeskBox와 관련이 없습니다.
