using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace OneTaskBar
{
    // Some tray apps ignore a synthetic single click and open only on a real double click.
    // For those known apps, starting the executable again asks the running copy to show its window.
    internal static class TrayWake
    {
        static readonly string[] singleInstance={"KakaoTalk"};
        internal static bool Applies(string executable)
        {
            if(String.IsNullOrEmpty(executable))return false;
            string name=Path.GetFileNameWithoutExtension(executable);
            foreach(string known in singleInstance)if(String.Equals(known,name,StringComparison.OrdinalIgnoreCase))return true;
            return false;
        }
        internal static void AfterClick(string executable)
        {
            if(!Applies(executable)||!File.Exists(executable))return;
            Task.Run(()=>
            {
                try
                {
                    Thread.Sleep(700);
                    if(HasWindow(Path.GetFileNameWithoutExtension(executable)))return;
                    Process.Start(new ProcessStartInfo(executable) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(executable)});
                }
                catch(Exception error){AppLog.Once("tray-wake",error.GetType().Name);}
            });
        }
        static bool HasWindow(string processName)
        {
            bool found=false;
            foreach(var process in Process.GetProcessesByName(processName))
            {
                using(process)
                {
                    uint id=(uint)process.Id;
                    Native.EnumWindows((window,state)=>
                    {
                        uint owner;
                        Native.GetWindowThreadProcessId(window,out owner);
                        if(owner!=id||!Native.IsWindowVisible(window)||Native.IsIconic(window))return true;
                        Native.RectI rect;
                        if(Native.GetWindowRect(window,out rect)&&rect.Right-rect.Left>=200&&rect.Bottom-rect.Top>=200)found=true;
                        return !found;
                    },IntPtr.Zero);
                }
                if(found)return true;
            }
            return false;
        }
    }
}
