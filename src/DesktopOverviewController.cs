using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        internal DesktopOverview Overview;
        int overviewRequest;
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        internal async void OpenDesktopOverview()
        {
            TaskViewRequests++;
            if(Overview!=null&&Overview.IsVisible){Overview.Dismiss();return;}
            int request=++overviewRequest;
            Switcher.Cancel();PrepareWindowSwitcher();
            if(Demo)return;
            var foreground=Native.GetForegroundWindow();
            var monitor=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var apps=Running.ToArray();
            var value=await Task.Run(()=>VirtualDesktops.Read(apps,foreground));
            if(disposed||request!=overviewRequest)return;
            if(Overview!=null)Overview.Close();
            var panel=new DesktopOverview((id,window,operation)=>ApplyDesktopCommand(value,id,window,operation),window=>PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero));
            Overview=panel;panel.Closed+=(s,e)=>{if(Overview==panel)Overview=null;};
            panel.Present(value,monitor.Bounds);
        }
        async void ApplyDesktopCommand(DesktopSnapshot snapshot,Guid id,IntPtr window,int operation)
        {
            int index=snapshot.Pages.FindIndex(p=>p.Id==id);
            if(index<0||disposed)return;
            await Task.Delay(Motion.Enabled?180:30);
            bool changed=await Task.Run(()=>SystemPanels.DesktopCommand(index-snapshot.CurrentIndex,operation));
            if(changed&&window!=IntPtr.Zero&&Native.IsWindow(window))WindowActivation.Activate(window);
            if(!changed)AppLog.Write("desktop-command","Windows did not accept the desktop shortcut");
        }
        void CloseDesktopOverview()
        {
            overviewRequest++;
            if(Overview!=null){Overview.Close();Overview=null;}
        }
    }
}
