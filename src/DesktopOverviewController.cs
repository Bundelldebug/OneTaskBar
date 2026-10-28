using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Interop;

namespace OneTaskBar
{
    internal sealed partial class ShellController
    {
        internal DesktopOverview Overview;
        int overviewRequest;
        DesktopSnapshot overviewCache;
        DateTime overviewCachedAt;
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wparam,IntPtr lparam);
        internal async void OpenDesktopOverview()
        {
            TaskViewRequests++;
            if(Overview!=null&&Overview.IsVisible&&!Overview.Retiring){overviewRequest++;Overview.Dismiss();return;}
            int request=++overviewRequest;
            Switcher.Cancel();PrepareWindowSwitcher(true);
            if(Demo)return;
            var foreground=Native.GetForegroundWindow();
            var monitor=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var apps=Running.ToArray();
            if(Overview!=null&&(Overview.Retiring||Overview.IsClosed)){Overview.Close();Overview=null;}
            var clock=Stopwatch.StartNew();DesktopSnapshot value=null;
            var panel=Overview;
            if(panel==null){panel=new DesktopOverview((id,window,operation)=>{if(overviewCache!=null)ApplyDesktopCommand(overviewCache,id,window,operation);},window=>PostMessage(window,0x10,IntPtr.Zero,IntPtr.Zero));Overview=panel;panel.Closed+=(s,e)=>{if(Overview==panel)Overview=null;};}
            bool cached=overviewCache!=null&&DateTime.UtcNow-overviewCachedAt<TimeSpan.FromSeconds(2)&&overviewCache.Current==VirtualDesktops.CurrentId();
            if(cached)value=overviewCache;
            else{panel.ShowLoading(monitor.Bounds);value=await Task.Run(()=>VirtualDesktops.Read(apps,foreground));}
            if(disposed||request!=overviewRequest||Overview!=panel||panel.Retiring)return;
            overviewCache=value;overviewCachedAt=DateTime.UtcNow;
            panel.Present(value,monitor.Bounds);
            AppLog.Json("desktop-overview-timing.json",new {readyMilliseconds=clock.Elapsed.TotalMilliseconds,buildMilliseconds=panel.BuildMilliseconds,layoutPasses=panel.LayoutPasses,cached=cached,desktops=value.Pages.Count,windows=value.Pages.Sum(p=>p.Windows.Count)});
        }
        async void ApplyDesktopCommand(DesktopSnapshot snapshot,Guid id,IntPtr window,int operation)
        {
            int index=snapshot.Pages.FindIndex(p=>p.Id==id);
            if(index<0||disposed)return;
            await Task.Delay(Motion.Enabled?180:30);
            overviewCache=null;
            bool changed=await Task.Run(()=>{
                int steps;
                if(operation==1)return SystemPanels.DesktopCommand(0,1);
                if(!VirtualDesktops.CurrentDistance(id,out steps))return operation==0&&snapshot.Pages.Count==1&&snapshot.Current==id;
                return SystemPanels.DesktopCommand(steps,operation);
            });
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
