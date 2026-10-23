using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace OneTaskBar
{
    internal static class AdminLaunch
    {
        internal static ProcessStartInfo Prepare(AppEntry app)
        {
            if(app==null||(app.AppID??"").Contains("!"))return null;
            string target=app.Target;
            if(!Supported(target))target=app.Executable;
            if(!Supported(target)&&!String.IsNullOrEmpty(app.AppID)){var shortcut=WebAppIcons.LaunchShortcut(app.AppID,app.Name);if(shortcut!=null&&Supported(shortcut.Target)&&shortcut.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))target=shortcut.File;else if(Supported(app.AppID)&&Path.IsPathRooted(app.AppID))target=app.AppID;}
            if(!Supported(target))return null;
            // ShellExecute preserves a shortcut's arguments and working directory.
            return new ProcessStartInfo(target){UseShellExecute=true,Verb="runas"};
        }
        static bool Supported(string target)
        {
            if(String.IsNullOrWhiteSpace(target))return false;try{Uri uri;if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&!uri.IsFile)return false;string extension=Path.GetExtension(target);return (extension.Equals(".exe",StringComparison.OrdinalIgnoreCase)||extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase))&&!target.Contains("://");}catch(ArgumentException){return false;}
        }
        internal static void Start(AppEntry app,ShellController shell)
        {
            var info=Prepare(app);if(info==null)return;
            try{using(var process=Process.Start(info)){} }
            catch(Win32Exception error){if(error.NativeErrorCode!=1223)Failed(shell,error);}
            catch(Exception error){Failed(shell,error);}
        }
        static void Failed(ShellController shell,Exception error){AppLog.Write("launch-admin",error.Message);shell.ShowWelcome();shell.Welcome.Feedback("관리자 권한으로 실행하지 못했습니다. 앱의 경로와 Windows 권한을 확인해 주세요.");}
        internal static int Probe()
        {
            try{var apps=Catalog.Read(false);var desktop=apps.Where(a=>!String.IsNullOrEmpty(a.AppID)&&!a.AppID.Contains("!")&&!String.IsNullOrEmpty(a.Target)&&a.Target.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)&&File.Exists(a.Executable)).ToList();bool passed=desktop.Count>0&&desktop.All(a=>{var launch=Prepare(a);return launch!=null&&launch.FileName==a.Target&&launch.Verb=="runas"&&launch.UseShellExecute;});AppLog.Json("admin-targets-probe.json",new {passed=passed,resolvedDesktopApps=desktop.Count,samples=desktop.Take(10).Select(a=>new {name=a.Name,shortcutPreserved=Prepare(a)!=null&&Prepare(a).FileName==a.Target}).ToArray(),samsungBrowserResolved=desktop.Any(a=>a.AppID=="Internet"),uacInvoked=false,appsLaunched=false});return passed?0:1;}catch(Exception error){AppLog.Json("admin-targets-probe.json",new {passed=false,error=error.ToString()});return 1;}
        }
    }
}
