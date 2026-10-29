# One TaskBar

Windows 작업표시줄을 독, 시작 메뉴, 검색과 빠른 설정으로 바꾸는 앱입니다. [English](README.md)

[정식 다운로드](https://github.com/Bundelldebug/OneTaskBar/releases/latest) · [베타 및 이전 버전](https://github.com/Bundelldebug/OneTaskBar/releases)

설치는 `OneTaskBar-Setup.exe`, 무설치는 포터블 ZIP의 압축을 모두 푼 뒤 실행하세요. 미서명 빌드이므로 Windows SmartScreen이나 조직 정책에 따라 실행이 차단될 수 있습니다.

## 주요 기능

- 독을 화면 네 방향에 배치하고 앱 순서와 배경을 바꿀 수 있습니다.
- 앱·파일 검색과 앱 폴더를 지원합니다. Windows 시작 메뉴 폴더로 정리하는 옵션도 있습니다.
- 빠른 설정 항목을 이동하고 크기를 조절할 수 있습니다. 정식은 UI 8.5·9.0, 베타는 9.5 유리 UI도 제공합니다.
- 미디어 캡슐과 빠른 설정에서 재생·출력 기기를 조절할 수 있습니다. 밝기·음량의 숫자 표시 옵션은 기본 꺼짐입니다.
- 창·데스크톱 미리보기와 알림 묶기·펼치기·밀어서 삭제를 지원합니다.
- 시작·검색·알림·Alt+Tab을 Windows 기본 화면으로 사용할 수도 있습니다.

시작 버튼을 다시 누르거나 메뉴 바깥을 클릭하면 닫힙니다. 앱 우클릭 메뉴에서 고정, 폴더와 지원되는 데스크톱 앱의 관리자 실행을 사용할 수 있습니다.

## 단축키

| 단축키 | 기능 |
| --- | --- |
| Ctrl+Alt+H | One TaskBar 설정 |
| Ctrl+Alt+Q | 빠른 설정 |
| Ctrl+Shift+Tab+1 | One TaskBar 종료 및 Windows 작업표시줄 복원 |

별도 복구 프로그램이 앱 종료나 멈춤을 감지해 Windows 작업표시줄을 복원합니다. 긴급 종료 단축키는 설정에서 끌 수 있습니다.

## 연동과 데이터

자체 알림은 Windows의 알림 접근 권한과 선택적인 로컬 ID 등록이 필요합니다. [알림 설정 안내](docs/NOTIFICATIONS.md)를 참고하세요. 삼성 기능은 PC에 설치된 앱과 서비스를 사용하며, 외부 성능 모드 변경을 지원하지 않는 기기에는 삼성 설정 바로가기를 제공합니다.

YouTube 고화질 커버는 기본 꺼짐입니다. 허용하면 현재 재생 제목과 일치하는 삼성 브라우저의 로컬 기록을 확인해 `i.ytimg.com`에서 커버를 가져옵니다. 제목·쿠키·기록 내용은 전송하지 않습니다. 일치하는 영상이 없으면 미디어 앱의 원래 이미지를 사용합니다.

설정, 고정 앱, 검색어와 로그는 이 PC에 저장합니다. 별도 사용 정보 수집 서버는 없습니다.

## 빌드와 문제 해결

Windows x64, .NET Framework 4.8과 Windows PowerShell이 필요합니다.

```powershell
./Build.ps1
./tools/BuildInstaller.ps1 -SkipBuild
```

[디버깅 안내](docs/DEBUGGING.md) · [검사 결과와 알려진 제한](VALIDATION.md) · [유리 효과 구현](docs/GLASS_RENDERING.md)

CPU 측정만으로 배터리 사용 시간을 판단할 수 없습니다. 완전 방전 기준 사용 시간과 RAM 150 MiB 미만 목표는 아직 확인하지 못했습니다.

[MIT 라이선스](LICENSE)를 사용합니다. 설치된 타사 앱의 아이콘은 해당 앱에서 읽어 표시합니다. Microsoft·삼성·Apple과 무관한 독립 프로젝트입니다. [타사 저작권 고지](THIRD_PARTY_NOTICES.txt)
