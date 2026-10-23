using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using System.IO;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;
namespace OneTaskBar
{
    internal static class DockMotionChecks
    {
        static int Width(DockHost dock){Native.RectI bounds;Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out bounds);return bounds.Right-bounds.Left;}
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        static async Task<bool> Drag(DockHost dock,Button source,Point destination)
        {
            var owner=new WindowInteropHelper(dock.Apps).Handle;var origin=source.PointToScreen(new Point(source.ActualWidth/2,source.ActualHeight/2));var cursor=System.Windows.Forms.Cursor.Position;var hit=WindowFromPoint(new Native.PointI {X=(int)origin.X,Y=(int)origin.Y});AppLog.Write("drag-fixture","origin="+origin+" destination="+destination+" owner="+owner+" hit="+Native.GetAncestor(hit,2));
            return await Task.Run(()=>{bool down=false;try {if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)origin.X,Y=(int)origin.Y}),2)!=owner)return false;SetCursorPos((int)origin.X,(int)origin.Y);System.Threading.Thread.Sleep(35);mouse_event(2,0,0,0,UIntPtr.Zero);down=true;for(int i=1;i<=20;i++){double t=i/20.0;SetCursorPos((int)Math.Round(origin.X+(destination.X-origin.X)*t),(int)Math.Round(origin.Y+(destination.Y-origin.Y)*t));System.Threading.Thread.Sleep(20);}System.Threading.Thread.Sleep(120);return true;}finally{if(down){mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(150);}SetCursorPos(cursor.X,cursor.Y);}});
        }
        static async Task<bool> ReverseDrag(DockHost dock,Button source,Point destination)
        {
            var owner=new WindowInteropHelper(dock.Apps).Handle;var origin=source.PointToScreen(new Point(23,21));var cursor=System.Windows.Forms.Cursor.Position;
            return await Task.Run(()=>{bool down=false;try{
                if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)origin.X,Y=(int)origin.Y}),2)!=owner)return false;
                SetCursorPos((int)origin.X,(int)origin.Y);mouse_event(2,0,0,0,UIntPtr.Zero);down=true;double at=0;
                foreach(double endpoint in new[]{1.0,.1,.9,.2,1.0}){for(int i=1;i<=10;i++){double t=at+(endpoint-at)*i/10;SetCursorPos((int)Math.Round(origin.X+(destination.X-origin.X)*t),(int)Math.Round(origin.Y+(destination.Y-origin.Y)*t));System.Threading.Thread.Sleep(12);}at=endpoint;}
                System.Threading.Thread.Sleep(240);Func<double[]> offsets=()=>dock.ShownKeys.Select(k=>Canvas.GetLeft(dock.AppButton(k))+((TranslateTransform)dock.AppButton(k).RenderTransform).X).ToArray();var before=source.Dispatcher.Invoke(offsets);bool stable=true;
                for(int i=0;i<10;i++){SetCursorPos((int)destination.X+(i%2),(int)destination.Y);System.Threading.Thread.Sleep(24);var current=source.Dispatcher.Invoke(offsets);stable&=current.Length==before.Length&&current.Zip(before,(x,y)=>Math.Abs(x-y)<.75).All(v=>v);}
                return stable;
            }finally{if(down){mouse_event(4,0,0,0,UIntPtr.Zero);System.Threading.Thread.Sleep(150);}SetCursorPos(cursor.X,cursor.Y);}});
        }
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
                    var moving=(TranslateTransform)dock.AppButton("b").RenderTransform;dock.PreviewDock("e","a",false);await Task.Delay(70);bool livePreview=Math.Abs(moving.X)>.1;dock.EndDragPreview();await Task.Delay(380);bool cancelled=Math.Abs(moving.X)<.1&&String.Join(",",dock.ShownKeys)=="a,b,e";
                    bool ghostSafe;using(var ghostScope=new GhostScope(dock.AppButton("a"))){ghostSafe=ghostScope.Safe;}
                    shell.Pin(a,true);dock.Rebuild();await Task.Delay(380);shell.MoveDock("e","a",true);dock.Rebuild();await Task.Delay(380);bool pinByDrop=shell.Pins.Any(p=>p.Key=="e")&&Canvas.GetLeft(dock.AppButton("e"))<dock.PinBoundary&&Canvas.GetLeft(dock.AppButton("b"))>dock.PinBoundary;
                    shell.MoveDock("a","b",false);dock.Rebuild();await Task.Delay(380);bool unpinByDrop=!shell.Pins.Any(p=>p.Key=="a")&&String.Join(",",dock.ShownKeys)=="e,a,b";shell.MoveDockGroup("e",false);dock.Rebuild();await Task.Delay(380);bool emptyUnpinned=shell.Pins.Count==0&&dock.ShownKeys.All(k=>Canvas.GetLeft(dock.AppButton(k))>dock.PinBoundary);shell.MoveDockGroup("b",true);dock.Rebuild();await Task.Delay(380);bool emptyPinned=shell.Pins.Count==1&&shell.Pins[0].Key=="b"&&String.Join(",",dock.ShownKeys)=="b,a,e";
                    var saved=new JavaScriptSerializer().Deserialize<List<Catalog.PinPreference>>(File.ReadAllText(Path.Combine(AppLog.Data,"dock-pins.json")));bool persisted=saved.Any(p=>p.Key=="b"&&!p.Hidden)&&saved.Any(p=>p.Key=="a"&&p.Hidden)&&saved.Any(p=>p.Key=="e"&&p.Hidden);Program.Save(dock.Apps.Surface,(int)Math.Round(dock.Apps.ActualWidth),44,Path.Combine(AppLog.Data,"dock-pin-boundary.png"));
                    shell.Running.RemoveAll(p=>p.Key=="a"||p.Key=="b");dock.Rebuild();await Task.Delay(380);bool pinSurvivesClose=dock.AppButton("a")==null&&dock.AppButton("b")!=null;
                    Native.Place(dock.Apps,dock.Screen.Bounds,30,100,dock.Apps.Width,44);await Task.Delay(180);bool draggedIn=await Drag(dock,dock.AppButton("e"),dock.AppButton("b").PointToScreen(new Point(10,21)));await Task.Delay(400);bool actualPin=draggedIn&&shell.Pins.Any(p=>p.Key=="e");bool draggedOut=await Drag(dock,dock.AppButton("e"),dock.Apps.PointToScreen(new Point(dock.Apps.ActualWidth-4,22)));await Task.Delay(400);bool actualUnpin=draggedOut&&!shell.Pins.Any(p=>p.Key=="e")&&!DragReorder.Active;
                    shell.Running=new List<AppEntry>{a,c,d,e};dock.Rebuild();await Task.Delay(400);Native.Place(dock.Apps,dock.Screen.Bounds,30,100,dock.Apps.Width,44);await Task.Delay(100);bool reversed=await ReverseDrag(dock,dock.AppButton("e"),dock.AppButton("a").PointToScreen(new Point(8,21)));await Task.Delay(380);bool settled=reversed&&!DragReorder.Active&&String.Join(",",dock.ShownKeys)=="b,e,a,c,d"&&dock.ShownKeys.All(k=>Math.Abs(((TranslateTransform)dock.AppButton(k).RenderTransform).X)<.1);
                    result=grown>initial&&shrunk==initial&&up&&down&&retained&&ordered&&removed&&interrupted&&animation&&livePreview&&cancelled&&ghostSafe&&pinByDrop&&unpinByDrop&&emptyUnpinned&&emptyPinned&&persisted&&pinSurvivesClose&&actualPin&&actualUnpin&&settled?0:1;Program.Save(dock.Apps.Surface,(int)Math.Round(dock.Apps.ActualWidth),44,System.IO.Path.Combine(AppLog.Data,"dock-resize-preview.png"));AppLog.Json("dock-motion-probe.json",new {passed=result==0,rapidReversalsSettleWithoutNeighborJitter=settled,actualMouseDragPins=actualPin,actualMouseDragUnpins=actualUnpin,liveDragPreview=livePreview,cancelRestoresOrder=cancelled,dragGhostClickThrough=ghostSafe,dropPins=pinByDrop,dropUnpins=unpinByDrop,emptyRunningGroupDrop=emptyUnpinned,emptyPinnedGroupDrop=emptyPinned,pinPreferencesPersisted=persisted,pinnedAppSurvivesClose=pinSurvivesClose,animationsEnabled=Motion.Enabled,initialWidth=initial,grownWidth=grown,shrunkWidth=shrunk,growth=growth.ToArray(),shrink=shrink.ToArray(),intermediateFrames=middle,enterFade=faded,exitFade=exited,retainedButtons=retained,launchOrder=ordered,removedAfterExit=removed,interruptedResize=interrupted,finalOrder=dock.ShownKeys});
                }catch(Exception error){AppLog.Json("dock-motion-probe.json",new {passed=false,error=error.ToString()});}
                finally{AppLog.Json("drag-input.json",new {started=DragReorder.StartedMoves,completed=DragReorder.CompletedMoves});if(dock!=null)dock.Close();foreach(var window in fixtures)window.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        sealed class GhostScope : IDisposable
        {
            readonly DragReorder.DragGhost ghost;internal bool Safe;
            internal GhostScope(FrameworkElement source){ghost=new DragReorder.DragGhost(source);ghost.Show();ghost.Follow();int style=Native.GetWindowLong(new WindowInteropHelper(ghost).Handle,-20);Safe=(style&0x080000A0)==0x080000A0&&!ghost.IsHitTestVisible;}
            public void Dispose(){ghost.Close();}
        }
    }
}
