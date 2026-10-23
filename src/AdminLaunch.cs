using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace OneTaskBar
{
    internal static class AdminLaunch
    {
        internal static ProcessStartInfo Prepare(AppEntry app)
        {
            if(app==null||(app.AppID??"").Contains("!"))return null;
            string target=app.Target;
            if(!Supported(target))target=app.Executable;
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
    }
}
