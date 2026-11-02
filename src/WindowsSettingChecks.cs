using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OneTaskBar
{
    internal static class WindowsSettingChecks
    {
        static void Require(bool value,string message)
        {
            if(!value)throw new InvalidOperationException("Settings search: "+message);
        }
        internal static void Validate(List<string> checks)
        {
            var settings=LauncherSearch.Settings;
            foreach(string query in new[]{"하드 디스크 파티션 관리","파티션 포맷","disk management","ㄷㅅㅋ ㄱㄹ"})
            {
                var disk=settings.FirstOrDefault(item=>LauncherSearch.Matches(item.Name+" "+item.Keywords,query));
                Require(disk!=null&&disk.Executable=="mmc.exe"&&disk.Arguments.EndsWith("diskmgmt.msc\"",StringComparison.OrdinalIgnoreCase),"disk management aliases open the actual management console");
            }
            foreach(string query in new[]{"덮개를 닫을 때 설정","덮개 닫기","lid close"})
            {
                var lid=settings.FirstOrDefault(item=>LauncherSearch.Matches(item.Name+" "+item.Keywords,query));
                Require(lid!=null&&lid.Executable=="control.exe"&&lid.Arguments=="/name Microsoft.PowerOptions /page pageGlobalSettings","lid aliases select the power button and lid page");
            }
            foreach(string query in new[]{"장치 관리자","마이크 권한","환경 변수","파일 확장명","네트워크 어댑터","시작 프로그램"})
            {
                Require(settings.Any(item=>LauncherSearch.Matches(item.Name+" "+item.Keywords,query)),"modern settings and classic panel entries remain searchable: "+query);
            }
            Require(settings.Count(item=>item.AppSettings)==1&&settings.Single(item=>item.AppSettings).Name=="One TaskBar 설정","classic panel targets cannot be mistaken for the app's own settings");
            Require(settings.Select(item=>item.Name).Distinct().Count()==settings.Length,"catalog has no duplicate display entries");
            foreach(var item in settings.Where(item=>!item.AppSettings))
            {
                var launch=item.LaunchInfo();
                Require(launch.UseShellExecute&&String.IsNullOrEmpty(launch.Verb),"settings use normal Windows launch and elevation behavior");
                if(item.Executable==null)
                {
                    Require(launch.FileName.StartsWith("ms-settings:",StringComparison.Ordinal)&&launch.Arguments=="","modern settings use their registered page URI");
                }
                else
                {
                    Require(Path.GetDirectoryName(launch.FileName)==Environment.SystemDirectory&&File.Exists(launch.FileName),"classic targets use the real System32 executable rather than PATH lookup");
                }
            }
            Require(!settings.Any(item=>LauncherSearch.Matches(item.Name+" "+item.Keywords,"파티션 & calc.exe")),"query text cannot introduce another command");
            checks.Add("Windows and Control Panel search resolves Korean, English and initial aliases; disk management and lid settings keep separate fixed System32 targets, with native elevation and app-settings routing preserved");
        }
    }
}
