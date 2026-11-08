using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace OneTaskBar
{
    // Running with the administrator token lets the keyboard hook see Win and Alt+Tab while an elevated game has focus.
    // The sign-in task carries the setting; turning it on or off asks Windows for consent once, then restarts the app.
    internal static class AdminMode
    {
        internal static bool Enabled {get{return LogonStartup.IsAdministrator||LogonStartup.Elevated;}}
        // Returns a message for the settings window; null means the change did not happen.
        internal static string Set(bool on)
        {
            try
            {
                if(Enabled==on&&LogonStartup.IsAdministrator==on)return null;
                var info=new ProcessStartInfo(NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")),"--elevated-startup "+(on?"on":"off")){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
                int code;
                using(var helper=Process.Start(info)){helper.WaitForExit();code=helper.ExitCode;}
                if(code!=0)return "관리자 권한 설정을 저장하지 못했습니다";
                Restart();
                return on?"관리자 권한 실행을 켰습니다. 앱을 다시 시작합니다":"관리자 권한 실행을 껐습니다. 앱을 다시 시작합니다";
            }
            catch(Win32Exception error)
            {
                if(error.NativeErrorCode==1223)return "Windows 확인을 취소해 변경하지 않았습니다";
                AppLog.Once("admin-mode",error.Message);
                return "관리자 권한 설정을 저장하지 못했습니다";
            }
            catch(Exception error)
            {
                AppLog.Once("admin-mode",error.Message);
                return "관리자 권한 설정을 저장하지 못했습니다";
            }
        }
        // The sign-in task starts the new instance once this one has released its single-instance lock.
        static void Restart()
        {
            var launch=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),
                "/c ping -n 4 127.0.0.1 >nul & \""+Path.Combine(Environment.SystemDirectory,"schtasks.exe")+"\" /run /tn "+LogonStartup.TaskName)
                {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            using(Process.Start(launch)){}
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>Application.Current.Shutdown()),System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
