using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
namespace OneTaskBar
{
    internal static class SamsungPerformanceChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Samsung modes: "+message);}
        internal static void Validate(List<string> checks)
        {
            const string modes="[\"2\",\"1\",\"0\",\"3\",\"6\"]";var valid=SamsungPerformance.Parse(modes,1);var unknown=SamsungPerformance.Parse(modes,-1);
            Require(valid.Available&&valid.Modes.Count==4&&unknown.Available&&unknown.Selected==-1,"unknown current value must not remove modes");
            Require(valid.Modes.Select(m=>m.Id).SequenceEqual(new[]{0,1,2,3})&&SamsungPerformance.Parse(modes,6).Selected==3&&SamsungPerformance.Parse(modes,6).MaximumFan==true&&SamsungPerformance.Parse(modes,3,false).MaximumFan==false&&SamsungPerformance.Parse(modes,3,true).MaximumFan==true&&!SamsungPerformance.Parse(modes,3).MaximumFan.HasValue,"ordered modes and actual/unknown maximum-fan state");
            var missing=SamsungPerformance.Merge(false,null,-1,valid.Modes);var corrupt=SamsungPerformance.Merge(true,"{",2,valid.Modes);Require(missing.Available&&missing.Stale&&missing.Modes.Count==4&&missing.Selected==-1&&corrupt.Available&&corrupt.Selected==2&&corrupt.Stale,"transient unavailable or malformed capability retains known modes");
            Require(!SamsungPerformance.Merge(false,null,-1,null).Available&&!SamsungPerformance.Read(false).Available,"unsupported devices stay hidden");
            Require(SamsungPerformance.Response("{\"ModuleName\":\"ModulePerformance\",\"Action\":1,\"Key\":\"Support\",\"Output\":\"True\"}",1,"Support")=="True","OEM envelope parsing");
            bool denied=false,wrong=false;try{SamsungPerformance.Response("No Verify Client: Digital Sign check Fail",1,"Support");}catch(UnauthorizedAccessException){denied=true;}try{SamsungPerformance.Response("{\"ModuleName\":\"Other\",\"Action\":1,\"Key\":\"Support\"}",1,"Support");}catch(System.IO.InvalidDataException){wrong=true;}Require(denied&&wrong,"OEM rejection and mismatched envelope remain errors");
            checks.Add("Samsung modes survive transient current/capability reads, exclude unsupported flags and respect OEM client-signature rejection without launching its UI");
        }
        static int[] Windows(){var all=new List<int>();foreach(string name in new[]{"SamsungSettingsHost","SamsungSettings"})foreach(var process in Process.GetProcessesByName(name))using(process)all.Add(process.Id);return all.OrderBy(n=>n).ToArray();}
        internal static int Probe()
        {
            try{Validate(new List<string>());var before=SamsungPerformance.Read(true);int current=SamsungPerformance.Current();var windows=Windows();var access=before.Available?SamsungPerformance.Probe(true):SamsungDirectAccess.Unavailable;var after=SamsungPerformance.Read(true);bool preserved=current==SamsungPerformance.Current()&&windows.SequenceEqual(Windows());
                if(after.Available){var picker=new SamsungModePicker(after.Modes,_=>{}){Background=Paint.Brush("#FF5C605F")};picker.Refresh(after,false,access);Program.Save(picker,340,115,System.IO.Path.Combine(AppLog.Data,"samsung-performance-capsule.png"));var fan=SamsungPerformance.Parse("[\"0\",\"1\",\"2\",\"3\",\"6\"]",6);picker.Refresh(fan,false,SamsungDirectAccess.Ready);if(picker.FanText!="최대 팬 모드 · 켜짐")throw new Exception("Maximum fan display mismatch");Program.Save(picker,340,115,System.IO.Path.Combine(AppLog.Data,"samsung-performance-max-fan.png"));}
                AppLog.Json("samsung-mode-probe.json",new {passed=preserved,supportedModes=after.Modes.Select(m=>m.Id).ToArray(),capabilityAvailable=after.Available,directAccess=access.ToString(),directChangeSupported=access==SamsungDirectAccess.Ready,currentModeUnchanged=current==SamsungPerformance.Current(),samsungSettingsNotLaunched=windows.SequenceEqual(Windows()),modeChangeExecuted=false,registryWrites=false});return preserved?0:1;
            }catch(Exception error){AppLog.Json("samsung-mode-probe.json",new {passed=false,error=error.ToString()});return 1;}
        }
    }
}
