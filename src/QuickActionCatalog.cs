using System;
using System.Linq;
namespace OneTaskBar
{
    internal static class QuickActionCatalog
    {
        internal static readonly string[][] BaseSpecs={new[]{"mute","volume","음소거"},new[]{"theme","moon","다크 모드"},new[]{"dnd","dnd","방해 금지"},new[]{"efficiency","leaf","전력 절약"},new[]{"awake","window","화면 유지"},new[]{"wifi","wifi","Wi-Fi"},new[]{"bluetooth","bluetooth","블루투스"},new[]{"animations","play","독 애니메이션"},new[]{"startup","startup","자동 실행"},new[]{"slider-scroll","scroll","스크롤 조절"},new[]{"buds","headphones","Galaxy Buds"},new[]{"devices","devices","주변 기기 연결"},new[]{"windows","windows-settings","Windows 설정"},new[]{"capture","screenshot","화면 캡처"},new[]{"manager","task-manager","작업 관리자"},new[]{"ai-select","smart-select","AI 셀렉트"},new[]{"quick-share","quick-share","퀵 쉐어"},new[]{"second-screen","second-screen","세컨드 스크린"},new[]{"storage-share","storage-share","저장공간 공유"},new[]{"camera-share","camera-share","카메라 공유"}};
        internal static readonly string[][] Specs=BaseSpecs.Concat(QuickSettingCatalog.All.Select(s=>new[]{s.Key,s.Icon,s.Title})).ToArray();
        internal static readonly string[] Commands={"buds","devices","windows","capture","manager","ai-select","quick-share","second-screen","storage-share","camera-share"};
        internal static readonly string[] DefaultOutside=QuickActionCatalog.Commands.Concat(QuickSettingCatalog.All.Select(s=>s.Key)).ToArray();
        internal static void Invoke(string key,ShellController shell){switch(key){case "buds":shell.OpenQuickDevices(true);break;case "devices":shell.OpenQuickDevices(false);break;case "windows":shell.OpenWindowsSettings();break;case "capture":shell.OpenScreenCapture();break;case "manager":shell.OpenTaskManager();break;default:if(SamsungLink.Feature(key))shell.OpenSamsungFeature(key);break;}}
        internal static bool Available(string key,ShellController shell){return SamsungLink.Feature(key)?shell.SamsungFeature(key)!=null:key=="buds"?shell.GalaxyBuds()!=null:key!="devices"||shell.SamsungDevices()!=null;}
        // Caption under a Samsung tile: the linked phone for features that report one, otherwise empty.
        internal static string SamsungCaption(string key,ShellController shell)
        {
            if(!SamsungLink.Reports(key))return "";
            string phone=SamsungLink.Phone();
            return phone??"연결 안 됨";
        }
        internal static bool SamsungOn(string key){return SamsungLink.Reports(key)&&SamsungLink.Phone()!=null;}
    }
}
