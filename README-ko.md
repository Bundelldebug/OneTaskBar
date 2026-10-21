# One TaskBar

Windows 작업표시줄을 대체하는 독·런처·빠른 설정입니다. Windows 폰트와 설치된 앱의 원래 아이콘을 사용합니다. [English](README.md)

## 설치

[최신 릴리스](https://github.com/Bundelldebug/OneTaskBar/releases/latest)에서 **OneTaskBar-Setup.exe**를 받아 실행하고 설치를 누르세요. 자동 실행·바탕 화면 바로가기·설치 후 실행을 선택할 수 있습니다. 기본 설치와 사용에 관리자 권한이 필요하지 않으며 인증서 신뢰나 Windows 보안 정책을 바꾸지 않습니다.

- 실제 검증 환경: Windows 11 x64. Windows 10 2004 이상 x64와 .NET Framework 4.8도 호환 대상으로 삼지만 모든 Windows 빌드에서 셸 연동을 검증한 것은 아닙니다.
- 초기 배포 파일에는 코드 서명이 없습니다. Windows에서 알 수 없는 게시자 또는 SmartScreen 안내가 나올 수 있습니다. 릴리스의 SHA256SUMS로 파일을 확인할 수 있습니다.
- 프로그램: `%LOCALAPPDATA%\OneTaskBar\App`
- 설정: `%LOCALAPPDATA%\OneTaskBar\Data`
- 제거: Windows 설정 → 앱 → One TaskBar. 프로그램을 종료하면 Windows 작업표시줄이 복원되며, 제거해도 사용자 설정은 보관합니다.
- 기존 개인용 One UI 홈의 설정이 있으면 필요한 설정 파일만 한 번 가져옵니다. APK나 기존 로고는 가져오지 않습니다.

## 동작

설정에서 빠른 설정·알림센터·키보드 시작 키·검색을 각각 Windows 기본 화면 또는 One TaskBar 화면으로 고를 수 있습니다. 앱 화면에서 바로 입력하면 검색으로 넘어가며 한글 조합 중에는 기다립니다. 터치 스크롤과 트랙패드의 미세 스크롤을 지원합니다.

빠른 설정에는 실제 음량·지원되는 화면의 밝기·Wi-Fi·블루투스·음악 재생·전원 모드 등이 연결됩니다. 지원하지 않는 장치나 설치되지 않은 삼성 앱 버튼은 자연스럽게 숨깁니다. 배터리가 없는 PC에서는 배터리 부분을 숨기거나 직접 입력한 1~9999 W를 표시할 수 있습니다. 이 숫자는 실시간 전력 측정값이 아닙니다.

기본 설치는 패키지 등록이 필요 없는 **Windows 알림센터**를 사용합니다. 자체 알림센터에서 Windows 알림을 읽고 지우려면 별도의 패키지 ID와 Windows의 알림 접근 허용이 필요합니다. v0.3.0 설치기는 개발용 미서명 패키지를 자동 등록하지 않습니다. 개발용 선택 절차와 권한 설명은 [알림 연결 문서](docs/NOTIFICATIONS.md)에 있습니다.

## 단축키와 복구

| 단축키 | 기능 |
| --- | --- |
| Ctrl+Alt+H | One TaskBar 설정 |
| Ctrl+Alt+Q | 빠른 설정 |
| Ctrl+Shift+Tab+1 | **One TaskBar 자체를 강제 종료**하고 Windows 작업표시줄 복원 |

긴급 종료는 설정에서 끌 수 있습니다. 독립 복구 도우미가 감시하므로 UI가 응답하지 않아도 사용할 수 있습니다. 현재 사용 중인 다른 앱을 종료하는 기능은 아닙니다. Windows 보안 화면이나 도우미 자체까지 종료된 상황에서는 단축키를 처리할 수 없습니다.

## 빌드와 공개 자료

Windows PowerShell에서 `Build.ps1`, 이어서 `tools/BuildInstaller.ps1 -SkipBuild`를 실행하면 앱과 설치기를 만듭니다. NuGet이나 별도 SDK 다운로드가 필요하지 않습니다. 전체 빌드·검증 방법은 [README](README.md), 측정 결과는 [VALIDATION.md](VALIDATION.md)를 참고하세요.

원본 아이콘과 소스는 MIT 라이선스입니다. Samsung APK·추출 로고·배경화면·폰트·개인 스크린샷과 영상은 배포하지 않습니다. 설치된 다른 앱의 로고는 실행 중 해당 PC에서 읽으며 권리는 각 소유자에게 있습니다. Microsoft·Samsung과 무관한 독립 프로젝트이고, 제3자의 권리 주장 가능성이 전혀 없다고 보증하는 것은 아닙니다.
