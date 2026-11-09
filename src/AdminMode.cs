using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace OneTaskBar
{
    // Administrator windows (an elevated game) can only be switched to, restored and watched for Alt+Tab by an administrator process.
    // A separate helper does that, so the app itself stays a normal process and can keep its notification connection.
    // Turning the helper on or off asks Windows for consent once, then restarts the app.
    internal static class AdminMode
    {
        internal static bool Enabled {get{return LogonStartup.AdminHelperRegistered||LogonStartup.IsAdministrator||LogonStartup.Elevated;}}
        // Returns a message for the settings window; null means the change did not happen.
        // Runs off the interface thread: the Windows consent window can stay open as long as it likes without freezing the app.
        internal static System.Threading.Tasks.Task<string> SetAsync(bool on)
        {
            return System.Threading.Tasks.Task.Run(()=>Set(on));
        }
        internal static string Set(bool on)
        {
            try
            {
                bool legacy=LogonStartup.IsAdministrator;
                if(LogonStartup.AdminHelperRegistered==on&&!legacy&&!LogonStartup.Elevated)return null;
                if(!on)AdminBridge.Quit();
                var info=new ProcessStartInfo(NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")),"--elevated-startup "+(on?"on":"off")){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
                int code;
                using(var helper=Process.Start(info))
                {
                    // Waiting for the consent window is bounded, so a closed or hidden prompt cannot leave the setting stuck.
                    if(!helper.WaitForExit(180000))return "Windows 확인이 끝나지 않아 변경하지 않았습니다";
                    code=helper.ExitCode;
                }
                if(code!=0)return "관리자 권한 설정을 저장하지 못했습니다";
                // The helper is started or stopped right now; the app itself keeps running. Only an app that was itself running as
                // administrator (an older setting) has to start again, to become a normal process.
                AdminBridge.Refresh();
                if(legacy)
                {
                    Restart();
                    return on?"관리자 권한 창 지원을 켰습니다. 앱을 다시 시작합니다":"관리자 권한 창 지원을 껐습니다. 앱을 다시 시작합니다";
                }
                return on?"관리자 권한 창 지원을 켰습니다":"관리자 권한 창 지원을 껐습니다";
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
        // A copy of this program waits for the current process to exit, then runs the task; no shell or delay trick is involved.
        static void Restart()
        {
            int id=Process.GetCurrentProcess().Id;
            var launch=new ProcessStartInfo(NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")),"--run-startup-after "+id)
                {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            using(Process.Start(launch)){}
            Application.Current.Dispatcher.BeginInvoke(new Action(()=>Application.Current.Shutdown()),System.Windows.Threading.DispatcherPriority.Background);
        }
        // Entry point of that waiting copy. It never loads the interface.
        internal static int RunAfter(string text)
        {
            int id;
            if(!Int32.TryParse(text,out id)||id<=0)return 2;
            try
            {
                using(var parent=Process.GetProcessById(id))
                {
                    if(!parent.WaitForExit(15000))return 3;
                }
            }
            catch(ArgumentException){}
            try
            {
                var run=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"schtasks.exe"),"/run /tn "+LogonStartup.TaskName)
                    {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
                using(var task=Process.Start(run))
                {
                    task.WaitForExit(10000);
                    return task.HasExited?task.ExitCode:4;
                }
            }
            catch(Exception error)
            {
                AppLog.Once("admin-restart",error.Message);
                return 1;
            }
        }
    }
}
