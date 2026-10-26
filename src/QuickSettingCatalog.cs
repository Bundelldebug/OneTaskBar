using System;
using System.Collections.Generic;
using System.Linq;
namespace OneTaskBar
{
    internal sealed class QuickSetting
    {
        internal readonly string Key,Title,Icon;
        internal readonly Func<PreferenceData,bool> Read;
        internal readonly Action<PreferenceData,bool> Write;
        internal QuickSetting(string key,string title,Func<PreferenceData,bool> read,Action<PreferenceData,bool> write){Key=key;Title=title;Icon="pref-"+key;Read=read;Write=write;}
    }
    internal static class QuickSettingCatalog
    {
        internal static readonly QuickSetting[] All={
            new QuickSetting("blur","동적 블러",p=>p.Blur,(p,v)=>p.Blur=v),
            new QuickSetting("charge-motion","충전 애니메이션",p=>p.ChargeAnimation,(p,v)=>p.ChargeAnimation=v),
            new QuickSetting("hover","호버 확대",p=>p.Hover,(p,v)=>p.Hover=v),
            new QuickSetting("running-dots","실행 앱 표시",p=>p.RunningDots,(p,v)=>p.RunningDots=v),
            new QuickSetting("seconds","시계 초 표시",p=>p.Seconds,(p,v)=>p.Seconds=v),
            new QuickSetting("notification-popups","알림 팝업",p=>p.NotificationPopups,(p,v)=>p.NotificationPopups=v),
            new QuickSetting("buds-popup","버즈 연결 팝업",p=>p.BudsConnectionPopups,(p,v)=>p.BudsConnectionPopups=v),
            new QuickSetting("dock-theme","독 색 모드 분리",p=>p.SeparateDockTheme,(p,v)=>p.SeparateDockTheme=v),
            new QuickSetting("solid-dock","독 단색 배경",p=>p.DockSolidBackground,(p,v)=>{p.DockSolidBackground=v;p.DockBackgroundKind=v?1:0;}),
            new QuickSetting("duo","듀오 상태 아이콘",p=>p.DuoStatusIcon,(p,v)=>p.DuoStatusIcon=v),
            new QuickSetting("numbers","밝기·음량 수치",p=>p.SliderValueNumbers,(p,v)=>p.SliderValueNumbers=v),
            new QuickSetting("window-drop","드래그 분할·팝업",p=>p.DockWindowDrop,(p,v)=>p.DockWindowDrop=v),
            new QuickSetting("cover","큰 미디어 커버",p=>p.NowBarLargeCover,(p,v)=>p.NowBarLargeCover=v),
            new QuickSetting("charge-sound","충전 연결음",p=>p.ChargeSoundEnabled,(p,v)=>p.ChargeSoundEnabled=v),
            new QuickSetting("emergency-exit","긴급 종료 단축키",p=>p.EmergencyExit,(p,v)=>p.EmergencyExit=v),
            new QuickSetting("native-start","Windows 시작 키",p=>p.NativeStartKey,(p,v)=>p.NativeStartKey=v),
            new QuickSetting("native-search","Windows 검색",p=>p.NativeSearch,(p,v)=>p.NativeSearch=v),
            new QuickSetting("native-switcher","Windows Alt+Tab",p=>p.NativeAltTab,(p,v)=>p.NativeAltTab=v),
            new QuickSetting("native-center","Windows 알림센터",p=>p.NativeNotifications,(p,v)=>p.NativeNotifications=v),
            new QuickSetting("native-quick","Windows 빠른 설정",p=>p.NativeQuickSettings,(p,v)=>p.NativeQuickSettings=v),
            new QuickSetting("native-button","Windows 시작 버튼",p=>p.NativeStartButton,(p,v)=>p.NativeStartButton=v),
            new QuickSetting("notification-sync","알림 연동",p=>p.NotificationIntegration,(p,v)=>p.NotificationIntegration=v)
        };
        static readonly Dictionary<string,QuickSetting> byKey=All.ToDictionary(s=>s.Key,StringComparer.Ordinal);
        internal static QuickSetting Find(string key){QuickSetting setting;return byKey.TryGetValue(key,out setting)?setting:null;}
    }
}
