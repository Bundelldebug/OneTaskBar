using System;
using System.Collections.Generic;
using System.IO;

namespace OneTaskBar
{
    // Search words never become executable names or command arguments.
    internal static class WindowsSettingCatalog
    {
        static SearchSetting Page(string name,string keywords,string icon,string page)
        {
            return new SearchSetting(name,keywords+" Windows 설정 settings",icon,"ms-settings:"+page);
        }
        static SearchSetting Panel(string name,string keywords,string icon,string canonical,string page=null)
        {
            return Command(name,keywords,icon,"control.exe","/name Microsoft."+canonical+(page==null?"":" /page "+page));
        }
        static SearchSetting Command(string name,string keywords,string icon,string executable,string arguments)
        {
            return new SearchSetting(name,keywords+" 제어판 control panel",icon,null,executable,arguments);
        }
        static SearchSetting Console(string name,string keywords,string icon,string file)
        {
            return Command(name,keywords,icon,"mmc.exe","\""+Path.Combine(Environment.SystemDirectory,file)+"\"");
        }
        internal static IEnumerable<SearchSetting> Items()
        {
            yield return Page("고급 디스플레이","새로 고침 빈도 주사율 모니터 refresh rate HDR","window","display-advanced");
            yield return Page("야간 모드","블루라이트 night light 색온도","brightness","nightlight");
            yield return Page("저장소","디스크 공간 임시 파일 정리 storage sense","folder","storagesense");
            yield return Page("클립보드","복사 붙여넣기 기록 동기화 clipboard history","window","clipboard");
            yield return Page("시스템 정보","PC 사양 컴퓨터 이름 RAM 프로세서 about device specs","window","about");
            yield return Page("멀티태스킹","창 맞춤 스냅 가상 데스크톱 multitasking snap","window","multitasking");
            yield return Page("기본 앱","파일 연결 브라우저 default apps 확장자","apps","defaultapps");
            yield return Page("시작 프로그램","로그인 자동 시작 startup apps 부팅","apps","startupapps");
            yield return Page("선택적 기능","Windows 기능 optional features 언어 팩","apps","optionalfeatures");
            yield return Page("이더넷","유선랜 인터넷 Ethernet IP DNS","ethernet","network-ethernet");
            yield return Page("VPN","가상 사설망 네트워크 vpn","wifi","network-vpn");
            yield return Page("프록시","인터넷 proxy 서버","wifi","network-proxy");
            yield return Page("모바일 핫스팟","인터넷 공유 테더링 mobile hotspot","wifi","network-mobilehotspot");
            yield return Page("비행기 모드","무선 airplane mode","wifi","network-airplanemode");
            yield return Page("프린터 및 스캐너","인쇄 printer scanner 장치 추가","window","printers");
            yield return Page("마우스","커서 포인터 mouse 스크롤","mouse","mousetouchpad");
            yield return Page("터치패드","트랙패드 제스처 touchpad trackpad","mouse","devices-touchpad");
            yield return Page("배경 화면","바탕화면 사진 wallpaper background","window","personalization-background");
            yield return Page("색 및 테마 모드","강조 색상 다크 라이트 color dark light","window","colors");
            yield return Page("잠금 화면","잠금화면 lock screen 배경","window","lockscreen");
            yield return Page("Windows 작업표시줄","taskbar 작업 표시줄 정렬 숨기기","window","taskbar");
            yield return Page("Windows 시작 메뉴","start 추천 항목 시작 화면","apps","personalization-start");
            yield return Page("테마","테마 바탕화면 theme 소리","window","themes");
            yield return Page("사용자 정보","계정 사진 프로필 account your info","window","yourinfo");
            yield return Page("로그인 옵션","암호 PIN 지문 얼굴 Windows Hello sign in","window","signinoptions");
            yield return Page("다른 사용자","계정 추가 가족 other users","window","otherusers");
            yield return Page("날짜 및 시간","시계 시간대 date time timezone","window","dateandtime");
            yield return Page("언어 및 지역","한국어 영어 language region 키보드 언어","keyboard","regionlanguage");
            yield return Page("접근성 디스플레이","텍스트 크기 애니메이션 접근성 accessibility","window","easeofaccess-display");
            yield return Page("돋보기","화면 확대 magnifier zoom","search","easeofaccess-magnifier");
            yield return Page("내레이터","화면 읽기 narrator 스크린 리더","volume","easeofaccess-narrator");
            yield return Page("접근성 키보드","고정키 필터키 화상 키보드 sticky filter keys","keyboard","easeofaccess-keyboard");
            yield return Page("대비 테마","고대비 high contrast 접근성","window","easeofaccess-highcontrast");
            yield return Page("위치 권한","위치 정보 location 개인정보 privacy","window","privacy-location");
            yield return Page("카메라 권한","웹캠 webcam camera 개인정보 privacy","window","privacy-webcam");
            yield return Page("마이크 권한","녹음 microphone 개인정보 privacy","volume","privacy-microphone");
            yield return Page("Windows 보안","백신 Defender 바이러스 방화벽 security","window","windowsdefender");
            yield return Page("Windows 업데이트","업데이트 update 보안 패치","window","windowsupdate");
            yield return Page("복구","PC 초기화 고급 시작 recovery reset","window","recovery");
            yield return Page("정품 인증","제품 키 라이선스 activation license","window","activation");
            yield return Page("문제 해결","troubleshoot 진단 복구 도구","settings","troubleshoot");

            yield return Console("하드 디스크 파티션 만들기 및 포맷","디스크 관리 파티션 관리 하드디스크 볼륨 축소 확장 disk management partition diskmgmt","folder","diskmgmt.msc");
            yield return Panel("덮개를 닫을 때의 작동 설정","노트북 덮개 닫기 전원 단추 lid close closing laptop power button","battery","PowerOptions","pageGlobalSettings");
            yield return Panel("전원 옵션","전원 관리 옵션 전력 절전 power options plans","battery","PowerOptions");
            yield return Panel("전원 관리 옵션 편집","절전 모드 시간 디스플레이 끄기 plan settings","battery","PowerOptions","pagePlanSettings");
            yield return Panel("프로그램 및 기능","프로그램 제거 삭제 변경 uninstall programs features","apps","ProgramsAndFeatures");
            yield return Command("Windows 기능 켜기/끄기","Hyper-V 가상화 WSL Windows features","apps","optionalfeatures.exe","");
            yield return Command("네트워크 연결","어댑터 설정 변경 IP DNS 속성 ncpa network adapter connections","wifi","control.exe","ncpa.cpl");
            yield return Panel("네트워크 및 공유 센터","네트워크 상태 공유 설정 network sharing center","wifi","NetworkAndSharingCenter");
            yield return Command("소리 제어판","재생 녹음 출력 입력 장치 사운드 속성 mmsys sound","volume","control.exe","mmsys.cpl");
            yield return Panel("마우스 속성","포인터 단추 휠 속도 mouse properties","mouse","Mouse");
            yield return Panel("키보드 속성","반복 속도 지연 keyboard properties","keyboard","Keyboard");
            yield return Panel("날짜 및 시간 제어판","인터넷 시간 동기화 date time clock","window","DateAndTime");
            yield return Panel("국가 또는 지역","날짜 시간 형식 로캘 유니코드 region locale","keyboard","RegionAndLanguage");
            yield return Panel("사용자 계정","계정 유형 관리자 암호 UAC user accounts","window","UserAccounts");
            yield return Panel("자격 증명 관리자","저장된 암호 credential manager","window","CredentialManager");
            yield return Panel("글꼴","글꼴 설치 삭제 font fonts","window","Fonts");
            yield return Panel("파일 탐색기 옵션","폴더 옵션 숨김 파일 확장명 표시 file explorer folder options","folder","FolderOptions");
            yield return Panel("인덱싱 옵션","검색 색인 index indexing options","search","IndexingOptions");
            yield return Panel("인터넷 옵션","브라우저 보안 연결 internet options","wifi","InternetOptions");
            yield return Panel("저장소 공간","저장소 풀 드라이브 storage spaces","folder","StorageSpaces");
            yield return Panel("파일 히스토리","파일 백업 이전 버전 file history backup","folder","FileHistory");
            yield return Command("고급 시스템 설정","성능 환경 변수 사용자 프로필 시작 및 복구 sysdm environment variables advanced system","window","control.exe","sysdm.cpl,,3");
            yield return Console("장치 관리자","드라이버 하드웨어 장치 device manager devmgmt","settings","devmgmt.msc");
            yield return Console("컴퓨터 관리","로컬 사용자 디스크 관리 computer management compmgmt","window","compmgmt.msc");
            yield return Console("서비스","Windows 서비스 자동 시작 services","settings","services.msc");
            yield return Console("이벤트 뷰어","Windows 로그 오류 기록 event viewer eventvwr","window","eventvwr.msc");
            yield return Console("작업 스케줄러","예약 작업 자동 실행 task scheduler taskschd","window","taskschd.msc");
            yield return Console("성능 모니터","시스템 모니터 리소스 performance monitor perfmon","window","perfmon.msc");
            yield return Console("고급 보안이 포함된 Windows 방화벽","방화벽 인바운드 아웃바운드 규칙 firewall","window","wf.msc");
        }
    }
}
