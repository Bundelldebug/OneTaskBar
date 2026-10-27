using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;

namespace OneTaskBar
{
    internal static class SystemPanelPlacement
    {
        internal static bool Supported(string process,string page)
        {
            if(page=="start")return String.Equals(process,"StartMenuExperienceHost",StringComparison.OrdinalIgnoreCase);
            return String.Equals(process,"ShellExperienceHost",StringComparison.OrdinalIgnoreCase)||String.Equals(process,"ShellHost",StringComparison.OrdinalIgnoreCase);
        }
        internal static bool Align(System.Drawing.Rectangle monitor,int edge,string page)
        {
            // Only move the native panel opened by the user's command. Do not
            // inspect other windows or keep a timer after the opening settles.
            IntPtr moved=IntPtr.Zero;
            for(int attempt=0;attempt<20;attempt++)
            {
                var handle=Native.GetForegroundWindow();
                uint pid;
                Native.GetWindowThreadProcessId(handle,out pid);
                bool matches=false;
                try {using(var process=Process.GetProcessById((int)pid))matches=Supported(process.ProcessName,page);}
                catch(ArgumentException) { }
                if(matches&&Native.IsWindowVisible(handle)&&!Native.IsIconic(handle))
                {
                    Native.RectI bounds;
                    if(Native.GetWindowRect(handle,out bounds)&&bounds.Right>bounds.Left&&bounds.Bottom>bounds.Top)
                    {
                        double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
                        double width=(bounds.Right-bounds.Left)/scale,height=(bounds.Bottom-bounds.Top)/scale;
                        var fit=DockGeometry.Popup(monitor,scale,width,height,edge);
                        if(page=="start")
                        {
                            var launcher=LauncherPlacement.Fit(monitor,Native.WorkArea(System.Windows.Forms.Screen.FromRectangle(monitor)),scale,width,height,edge);
                            fit=new Rect(launcher.X,launcher.Y,width,height);
                        }
                        var target=Native.PlacementBounds(monitor,scale,fit.X,fit.Y,width,height);
                        Native.SetWindowPos(handle,IntPtr.Zero,target.Left,target.Top,0,0,0x15);
                        moved=handle;
                        if(attempt>=5)return true;
                    }
                }
                Thread.Sleep(50);
            }
            return moved!=IntPtr.Zero;
        }
    }
}
