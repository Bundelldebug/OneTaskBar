using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    internal static class IslandSafetyChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Island safety: "+message);}
        internal static async Task<bool> Validate(Window source,Forms.Screen screen,double dpi)
        {
            if(Native.FindWindow("Shell_TrayWnd",null)==IntPtr.Zero)return false;
            var before=Native.WorkArea(screen);
            using(var area=new IslandSafeArea(screen,dpi))
            {
                area.SetActive(true);await Task.Delay(200);
                var reserved=Native.WorkArea(screen);
                int height=(int)Math.Ceiling(IslandSafeArea.Height*dpi);
                Require(reserved.Top>=before.Top+height&&reserved.Bottom==before.Bottom,"a real top appbar reserves the capsule band without changing the dock's bottom space");
                var host=DynamicIslandLayout.Host(screen.Bounds,area.PlacementArea(),dpi);
                Require(screen.Bounds.Top+(host.Top+28)*dpi<=reserved.Top+1,"compact media and feedback fit above application content");
                var capsule=new DynamicIsland(command=>{},value=>Task.FromResult(true),()=>{},id=>{});
                try
                {
                    capsule.Present(new MediaState(),null,screen,dpi,area.PlacementArea());
                    await Task.Delay(300);
                    Native.RectI actual;
                    Native.GetWindowRect(new WindowInteropHelper(capsule).Handle,out actual);
                    Require(Math.Abs(actual.Top-(screen.Bounds.Top+host.Top*dpi))<=1,"a newly created capsule window stays inside its reserved band at the monitor DPI; expected="+(screen.Bounds.Top+host.Top*dpi)+", actual="+actual.Top);
                    Native.SetWindowPos(new WindowInteropHelper(capsule).Handle,IntPtr.Zero,actual.Left,reserved.Top,0,0,0x15);
                    await Task.Delay(120);
                    Native.GetWindowRect(new WindowInteropHelper(capsule).Handle,out actual);
                    Require(Math.Abs(actual.Top-(screen.Bounds.Top+host.Top*dpi))<=1,"a deferred work-area position correction cannot move the capsule into application content");
                }
                finally{capsule.Close();}
                int stable=IslandSafeArea.ReservedPixels(screen.Bounds);
                for(int i=0;i<10;i++)area.SetActive(true);
                Require(IslandSafeArea.ReservedPixels(screen.Bounds)==stable&&Native.WorkArea(screen).Equals(reserved),"unchanged refreshes do not grow or rewrite the work area");
                source.WindowState=WindowState.Maximized;await Task.Delay(200);
                Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(source).Handle,out bounds);
                Require(bounds.Top>=reserved.Top-16*dpi,"the maximized title bar stays below the reserved band");
                source.WindowState=WindowState.Normal;
                area.SetActive(false);await Task.Delay(200);
                Require(Native.WorkArea(screen).Equals(before),"fullscreen removes the reservation completely");
                area.SetActive(true);await Task.Delay(150);
                Require(Native.WorkArea(screen).Top>=before.Top+height,"leaving fullscreen restores the safety area");
            }
            await Task.Delay(200);
            Require(Native.WorkArea(screen).Equals(before),"disabling the feature restores the exact previous work area");
            int originalEdge=Preferences.Current.DockEdge;
            bool originalBehind=Preferences.Current.MaximizedBehindDock;
            Preferences.Set(p=>{p.DockEdge=1;p.MaximizedBehindDock=false;});
            try
            {
                using(var area=new IslandSafeArea(screen,dpi))
                {
                    area.SetActive(true);
                    using(var dock=new AppBarReservation(screen,dpi))
                    {
                        await Task.Delay(200);
                        int combined=(int)Math.Round((IslandSafeArea.Height+68)*dpi);
                        Require(Native.WorkArea(screen).Top>=before.Top+combined,"the top dock and safety band reserve separate adjacent rows");
                        var rect=DockGeometry.Rect(screen.Bounds,dpi,1,new Rect(40,screen.Bounds.Height/dpi-56,220,44));
                        Require(rect.Top*dpi>=area.Bounds.Bottom-screen.Bounds.Top,"top dock icons stay below the island band");
                        Require(Math.Abs(DockGeometry.Reserved(screen.Bounds,Native.WorkArea(screen),dpi,1)-68)<2,"the safe band is excluded from the dock's balancing gap");
                        area.SetActive(false);
                        await Task.Delay(150);
                        dock.SetAutoHide(true);
                        area.SetActive(true);
                        dock.SetAutoHide(false);
                        await Task.Delay(150);
                        Require(area.Bounds.Top==before.Top&&Native.WorkArea(screen).Top>=before.Top+combined,"returning from fullscreen keeps the island above the top dock");
                    }
                }
            }
            finally{Preferences.Set(p=>{p.DockEdge=originalEdge;p.MaximizedBehindDock=originalBehind;});}
            await Task.Delay(200);
            Require(Native.WorkArea(screen).Equals(before),"top dock coexistence leaves no residual reservation");
            string childData=Path.Combine(AppLog.Data,"island-crash-child");
            Directory.CreateDirectory(childData);
            var start=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--island-space-fixture") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            start.EnvironmentVariables["ONE_TASKBAR_DATA"]=childData;
            Process child=null;
            try
            {
                child=Process.Start(start);
                string ready=Path.Combine(childData,"island-safe-ready.json");
                for(int i=0;i<160&&!File.Exists(ready)&&!child.HasExited;i++)await Task.Delay(50);
                Require(File.Exists(ready)&&!child.HasExited,"the disposable reservation child is ready");
                Require(Native.WorkArea(screen).Top>before.Top,"the child owns a real safety-area reservation");
                child.Kill();await Task.Run(()=>child.WaitForExit(3000));
                IslandSafeArea.Recover(childData);await Task.Delay(250);
                Require(Native.WorkArea(screen).Equals(before)&&!File.Exists(Path.Combine(childData,"island-safe-area.json")),"crash recovery removes only the expired reservation and restores workspace");
            }
            finally
            {
                if(child!=null){if(!child.HasExited){child.Kill();child.WaitForExit(3000);}child.Dispose();}
                IslandSafeArea.Recover(childData);
            }
            return true;
        }
        internal static int Hold()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            IslandSafeArea area=null;
            app.Dispatcher.BeginInvoke(new Action(()=>
            {
                var window=new Window();
                try
                {
                    var screen=Forms.Screen.PrimaryScreen;
                    double dpi=Native.PrepareMonitor(window,screen.Bounds);
                    area=new IslandSafeArea(screen,dpi);area.SetActive(true);
                    AppLog.Json("island-safe-ready.json",new {ready=true});
                }
                finally{window.Close();}
            }),DispatcherPriority.ApplicationIdle);
            try{app.Run();return 0;}finally{if(area!=null)area.Dispose();}
        }
    }
}
