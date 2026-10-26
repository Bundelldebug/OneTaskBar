using System;
using System.Linq;
namespace OneTaskBar
{
    internal static class QuickActionCatalog
    {
        internal static readonly string[][] BaseSpecs={new[]{"mute","volume","음소거"},new[]{"theme","moon","다크 모드"},new[]{"dnd","dnd","방해 금지"},new[]{"efficiency","leaf","전력 절약"},new[]{"awake","window","화면 유지"},new[]{"wifi","wifi","Wi-Fi"},new[]{"bluetooth","bluetooth","블루투스"},new[]{"animations","play","독 애니메이션"},new[]{"startup","startup","자동 실행"},new[]{"slider-scroll","scroll","스크롤 조절"},new[]{"buds","headphones","Galaxy Buds"},new[]{"devices","devices","주변 기기 연결"},new[]{"windows","windows-settings","Windows 설정"},new[]{"capture","screenshot","화면 캡처"},new[]{"manager","task-manager","작업 관리자"}};
        internal static readonly string[][] Specs=BaseSpecs.Concat(QuickSettingCatalog.All.Select(s=>new[]{s.Key,s.Icon,s.Title})).ToArray();
        internal static readonly string[] Commands={"buds","devices","windows","capture","manager"};
        internal static readonly string[] DefaultOutside=QuickActionCatalog.Commands.Concat(QuickSettingCatalog.All.Select(s=>s.Key)).ToArray();
        internal static void Invoke(string key,ShellController shell){switch(key){case "buds":shell.OpenQuickDevices(true);break;case "devices":shell.OpenQuickDevices(false);break;case "windows":shell.OpenWindowsSettings();break;case "capture":shell.OpenScreenCapture();break;case "manager":shell.OpenTaskManager();break;}}
        internal static bool Available(string key,ShellController shell){return key=="buds"?shell.GalaxyBuds()!=null:key!="devices"||shell.SamsungDevices()!=null;}
    }
}
