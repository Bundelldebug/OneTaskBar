using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace OneTaskBar
{
    // Windows blocks messages from this normal-privilege app to administrator windows, so a
    // minimized Task Manager cannot be restored and an administrator window cannot be closed directly.
    internal static class ElevatedWindows
    {
        [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
        const int AccessDenied=5,Cancelled=1223;
        // Replaced only by fixtures, so they start no real process.
        internal static Action<ProcessStartInfo> StartDispatch;

        internal static bool IsTaskManager(IntPtr window)
        {
            var kind=new StringBuilder(32);
            Native.GetClassName(window,kind,kind.Capacity);
            return kind.ToString()=="TaskManagerWindow";
        }
        // Task Manager runs one instance. Starting it again shows the existing window, minimized or not.
        internal static void ShowTaskManager()
        {
            Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"Taskmgr.exe")) {UseShellExecute=true});
        }
        // Asks normally first. Only an administrator window that refuses goes through a Windows permission prompt.
        internal static void Close(IntPtr window)
        {
            if(PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero))return;
            if(Marshal.GetLastWin32Error()!=AccessDenied)return;
            uint process;
            Native.GetWindowThreadProcessId(window,out process);
            if(process!=0)Start(CloseRequest(process));
        }
        internal static ProcessStartInfo CloseRequest(uint process)
        {
            return new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"taskkill.exe"),"/PID "+process) {UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden};
        }
        static void Start(ProcessStartInfo info)
        {
            if(StartDispatch!=null)
            {
                StartDispatch(info);
                return;
            }
            Task.Run(()=>
            {
                try
                {
                    using(Process.Start(info)){}
                }
                catch(Win32Exception error)
                {
                    if(error.NativeErrorCode!=Cancelled)AppLog.Once("elevated-window",error.NativeErrorCode.ToString());
                }
                catch(Exception error)
                {
                    AppLog.Once("elevated-window",error.GetType().Name);
                }
            });
        }
    }
}
