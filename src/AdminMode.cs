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
        const int Cancelled=1223,TimedOut=258;
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int process);
        internal static string Set(bool on)
        {
            // The helper is not started again behind the change while Windows is asking for consent.
            AdminBridge.Hold(true);
            try
            {
                bool legacy=LogonStartup.IsAdministrator;
                if(LogonStartup.AdminHelperRegistered==on&&!legacy&&!LogonStartup.Elevated)return null;
                if(!on)AdminBridge.Quit();
                int code=Elevate(on);
                if(code==Cancelled)return "Windows 확인을 취소해 변경하지 않았습니다";
                // What counts is the state Windows now reports, not how the administrator process happened to end.
                bool done=LogonStartup.AdminHelperRegistered==on&&!LogonStartup.Elevated;
                if(!on)AdminBridge.Quit();
                // The helper is started or stopped right now; the app itself keeps running. Only an app that was itself running as
                // administrator (an older setting) has to start again, to become a normal process.
                AdminBridge.Hold(false);
                AdminBridge.Refresh();
                if(!done)
                {
                    AppLog.Write("admin-mode","The change did not take effect; result "+code);
                    return code==TimedOut?"Windows 확인이 끝나지 않아 변경하지 않았습니다":"관리자 권한 설정을 저장하지 못했습니다";
                }
                if(legacy)
                {
                    Restart();
                    return on?"관리자 권한 창 지원을 켰습니다. 앱을 다시 시작합니다":"관리자 권한 창 지원을 껐습니다. 앱을 다시 시작합니다";
                }
                return on?"관리자 권한 창 지원을 켰습니다":"관리자 권한 창 지원을 껐습니다";
            }
            catch(Exception error)
            {
                AppLog.Once("admin-mode",error.Message);
                return "관리자 권한 설정을 저장하지 못했습니다";
            }
            finally{AdminBridge.Hold(false);}
        }
        static string ResultFile(string nonce){return Path.Combine(AppLog.Data,"admin-elevate-"+nonce+".txt");}
        // Asks Windows for consent and waits for the administrator process. Returns its exit code, Cancelled or TimedOut.
        static int Elevate(bool on)
        {
            string mode=on?"on":"off";
            IntPtr owner=Native.GetForegroundWindow();
            // A child of the packaged app would carry the package identity into the administrator process, which Windows ends
            // with a fail-fast (and which spins a processor core on some PCs). So a packaged app asks through a copy of itself
            // that Task Scheduler starts outside the package; that copy reports the result in a file.
            if(NotificationIdentity.IsCurrent)
            {
                string nonce=Guid.NewGuid().ToString("N"),result=ResultFile(nonce);
                AllowSetForegroundWindow(-1);
                if(LogonStartup.RunOnce("--elevate "+mode+" "+owner.ToInt64()+" "+nonce))
                {
                    try
                    {
                        var watch=Stopwatch.StartNew();
                        while(watch.Elapsed.TotalSeconds<190)
                        {
                            int code;
                            if(File.Exists(result)&&Int32.TryParse(File.ReadAllText(result).Trim(),out code))return code;
                            System.Threading.Thread.Sleep(200);
                        }
                        return TimedOut;
                    }
                    finally
                    {
                        LogonStartup.RemoveOnce();
                        try{File.Delete(result);}catch(Exception){}
                    }
                }
            }
            return Request(mode,owner);
        }
        static int Request(string mode,IntPtr owner)
        {
            var info=new ProcessStartInfo(NotificationIdentity.PhysicalFile(Path.Combine(AppLog.Root,"OneTaskBar.exe")),"--elevated-startup "+mode){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
            // The consent window belongs in front of the settings window the request came from.
            if(owner!=IntPtr.Zero)info.ErrorDialogParentHandle=owner;
            try
            {
                using(var helper=Process.Start(info))
                {
                    // Waiting for the consent window is bounded, so a closed or hidden prompt cannot leave the setting stuck.
                    if(!helper.WaitForExit(180000))return TimedOut;
                    return helper.ExitCode;
                }
            }
            catch(Win32Exception error)
            {
                if(error.NativeErrorCode==Cancelled)return Cancelled;
                throw;
            }
        }
        // Entry point of the copy Task Scheduler starts (--elevate on|off <owner window> <nonce>). It never loads the interface.
        internal static int Broker(string[] args)
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(args[3],"^[0-9a-f]{32}$"))return 2;
            int code=1;
            try
            {
                long owner;
                Int64.TryParse(args[2],out owner);
                code=Request(args[1]=="on"?"on":"off",new IntPtr(owner));
            }
            catch(Exception error){AppLog.Once("admin-elevate",error.Message);}
            try
            {
                string file=ResultFile(args[3]);
                File.WriteAllText(file+".tmp",code.ToString());
                File.Move(file+".tmp",file);
            }
            catch(Exception error){AppLog.Once("admin-elevate-result",error.GetType().Name);}
            return code;
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
                    // An app that was asked to close but is stuck is ended, so the normal one can start; otherwise nothing would come back.
                    if(!parent.WaitForExit(8000))
                    {
                        try{parent.Kill();}catch(Exception){}
                        if(!parent.WaitForExit(5000))return 3;
                    }
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
