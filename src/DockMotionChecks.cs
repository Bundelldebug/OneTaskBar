using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class DockMotionChecks
    {
        static int Width(DockHost dock){Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out bounds);return bounds.Right-bounds.Left;}
        internal static int Run()
        {
            RuntimeDockOrder.Check();var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;var fixtures=new List<Window>();ShellController shell=null;DockHost dock=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    shell=new ShellController(true);shell.Desktop=true;Func<string,long,AppEntry> item=(key,time)=>{var window=new Window {ShowInTaskbar=false,Width=100,Height=100};fixtures.Add(window);var entry=new AppEntry {Target=key,Name=key,StartedUtcTicks=time};entry.Windows.Add(new WindowInteropHelper(window).EnsureHandle());return entry;};var a=item("a",1);var b=item("b",2);var c=item("c",3);var d=item("d",4);var e=item("e",5);
                    shell.Running=new List<AppEntry>{b,a};dock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);dock.Show();await Task.Delay(60);int initial=Width(dock);var kept=dock.AppButton("b");shell.Running=new List<AppEntry>{c,a,b};dock.Rebuild();var growth=new List<int>();bool faded=false;for(int i=0;i<23;i++){await Task.Delay(18);growth.Add(Width(dock));if(i<10&&dock.AppButton("c").Opacity<.99)faded=true;}
                    int grown=Width(dock);bool retained=Object.ReferenceEquals(kept,dock.AppButton("b"));bool ordered=String.Join(",",dock.ShownKeys)=="a,b,c";shell.Running=new List<AppEntry>{b,a};dock.Rebuild();var shrink=new List<int>();bool exited=false;for(int i=0;i<23;i++){await Task.Delay(18);shrink.Add(Width(dock));var leaving=dock.AppButton("c");if(leaving!=null&&!leaving.IsHitTestVisible&&leaving.Opacity<.99)exited=true;}
                    bool removed=dock.AppButton("c")==null;int shrunk=Width(dock);shell.Running=new List<AppEntry>{d,a,b};dock.Rebuild();await Task.Delay(60);shell.Running=new List<AppEntry>{e,d,b,a};dock.Rebuild();await Task.Delay(55);shell.Running=new List<AppEntry>{b,e,a};dock.Rebuild();await Task.Delay(420);bool interrupted=String.Join(",",dock.ShownKeys)=="a,b,e"&&Width(dock)==grown;
                    bool up=growth.Zip(growth.Skip(1),(x,y)=>y>=x).All(v=>v),down=shrink.Zip(shrink.Skip(1),(x,y)=>y<=x).All(v=>v),middle=growth.Any(w=>w>initial&&w<grown)&&shrink.Any(w=>w>shrunk&&w<grown);bool animation=!Motion.Enabled||middle&&faded&&exited;
                    result=grown>initial&&shrunk==initial&&up&&down&&retained&&ordered&&removed&&interrupted&&animation?0:1;Program.Save(dock.Apps.Surface,(int)Math.Round(dock.Apps.ActualWidth),44,System.IO.Path.Combine(AppLog.Data,"dock-resize-preview.png"));AppLog.Json("dock-motion-probe.json",new {passed=result==0,animationsEnabled=Motion.Enabled,initialWidth=initial,grownWidth=grown,shrunkWidth=shrunk,growth=growth.ToArray(),shrink=shrink.ToArray(),intermediateFrames=middle,enterFade=faded,exitFade=exited,retainedButtons=retained,launchOrder=ordered,removedAfterExit=removed,interruptedResize=interrupted,finalOrder=dock.ShownKeys});
                }catch(Exception error){AppLog.Json("dock-motion-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(dock!=null)dock.Close();foreach(var window in fixtures)window.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
