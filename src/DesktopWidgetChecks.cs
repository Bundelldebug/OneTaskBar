using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class DesktopWidgetChecks
    {
        static void Require(bool value,string message)
        {
            if(!value)throw new Exception(message);
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            ShellController shell=null;
            Window owner=null;
            var hosts=new List<DesktopWidgetHost>();
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    var checks=new List<string>();
                    Require(!new PreferenceData().DesktopWidgets,"desktop widgets stay disabled by default");
                    string file=Path.Combine(AppLog.Data,"widget-layout-fixture.json");
                    var store=new DesktopWidgetStore(file);
                    var first=store.Add("clock-digital:2x2","fixture-a",new Size(1200,900));
                    var second=store.Add("gauges:2x1","fixture-a",new Size(1200,900));
                    Require(!new Rect(first.X,first.Y,first.Width,first.Height).IntersectsWith(new Rect(second.X,second.Y,second.Width,second.Height)),"adding widgets finds an early unoccupied position");
                    first.X=973;first.Y=719;first.Width=420;first.Height=240;
                    store.Save();
                    var reloaded=new DesktopWidgetStore(file);
                    Require(reloaded.Items.Count==2&&reloaded.Items[0].Id==first.Id&&reloaded.Items[0].X==973&&reloaded.Items[0].Width==420,"position, size, identity and monitor persist");
                    var fitted=DesktopWidgetStore.Fit(reloaded.Items[0],new Size(640,480));
                    Require(fitted.Right<=640&&fitted.Bottom<=480&&reloaded.Items[0].X==973,"smaller/disconnected displays fit without destroying the saved layout");
                    first.X=Double.NaN;first.Y=Double.PositiveInfinity;first.Width=-3;first.Height=Double.NaN;
                    DesktopWidgetStore.Normalize(first);
                    Require(!Double.IsNaN(first.X)&&!Double.IsInfinity(first.Y)&&first.Width>=100&&first.Height>=100,"invalid geometry is bounded");
                    store.Items.Clear();
                    for(int i=0;i<DesktopWidgetStore.Limit;i++)Require(store.Add("mute","fixture-a",new Size(1200,900))!=null,"bounded widget list accepts valid entries");
                    Require(store.Add("clock","fixture-a",new Size(1200,900))==null&&store.Add("unknown","fixture-a",new Size(1200,900))==null,"list limit and unknown commands are rejected");
                    File.WriteAllText(file,new JavaScriptSerializer().Serialize(new[]{new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key="unknown"}}));
                    Require(new DesktopWidgetStore(file).Items.Count==0,"unknown persisted commands cannot create widgets");
                    var area=new Size(1200,900);
                    var icon=new Rect(24,24,90,110);
                    var moved=DesktopWidgetStore.FindFree(new Rect(30,30,190,88),new List<Rect>{icon},area);
                    Require(!moved.IntersectsWith(icon)&&moved.X%DesktopWidgetStore.GridStep==0&&moved.Y%DesktopWidgetStore.GridStep==0,"a drop on an icon moves to a free grid position");
                    Require(Math.Abs(moved.X-30)+Math.Abs(moved.Y-30)<260,"the free position is the nearest one");
                    var snapped=DesktopWidgetStore.FindFree(new Rect(610,410,190,88),new List<Rect>(),area);
                    Require(snapped.X==600&&snapped.Y==408,"an open drop snaps to the nearest grid position");
                    var full=new Rect(0,0,1200,900);
                    Require(DesktopWidgetStore.FindFree(new Rect(30,30,190,88),new List<Rect>{full},area)==new Rect(30,30,190,88),"a full screen keeps the position instead of failing");
                    store.Items.Clear();
                    var beside=store.Add("clock-digital:2x1","fixture-a",area,new[]{icon});
                    Require(!new Rect(beside.X,beside.Y,beside.Width,beside.Height).IntersectsWith(icon),"new widgets avoid desktop icons");
                    foreach(string token in InfoTileCatalog.Tokens)
                    {
                        Require(DesktopWidgetCatalog.Known(token)&&DesktopWidgetCatalog.Title(token).Length>0&&DesktopWidgetCatalog.Minimum(token).Width<=DesktopWidgetCatalog.Initial(token).Width,"every tile size is a desktop widget");
                    }
                    checks.Add("bounded layout persistence, grid snap, icon avoidance, invalid data and disconnected display recovery");
                    foreach(double scale in new[]{1.0,1.25,1.5,2.0,2.5})
                    {
                        var slot=new DesktopWidgetSlot {Key="clock",X=5000,Y=5000,Width=260,Height=150};
                        var fit=DesktopWidgetStore.Fit(slot,new Size(1280/scale,720/scale));
                        var physical=new Rect(-1280+fit.X*scale,-720+fit.Y*scale,fit.Width*scale,fit.Height*scale);
                        Require(physical.Left>=-1280&&physical.Top>=-720&&physical.Right<=.01&&physical.Bottom<=.01,"negative-origin monitor and mixed DPI fit");
                    }
                    shell=new ShellController(true);
                    bool numbers=Preferences.Current.SliderValueNumbers;
                    await shell.InvokeQuickAction("numbers");
                    Require(Preferences.Current.SliderValueNumbers!=numbers&&!shell.QuickActionPending("numbers"),"desktop actions use the shared quick setting executor");
                    await shell.InvokeQuickAction("numbers");
                    Require(Preferences.Current.SliderValueNumbers==numbers,"shared quick setting executor restores its state");
                    checks.Add("quick setting command routing and pending cleanup");
                    owner=new Window {Title="Owned desktop widget fixture",Left=80,Top=80,Width=1000,Height=720,Background=Paint.Brush("#FF5C788A"),ShowInTaskbar=false};
                    owner.Show();
                    await Task.Delay(100);
                    IntPtr parent=new WindowInteropHelper(owner).Handle;
                    Native.RectI outer;
                    Native.GetWindowRect(parent,out outer);
                    double dpi=Native.GetDpiForWindow(parent)/96.0;
                    IntPtr foreground=Native.GetForegroundWindow();
                    shell.Power=new PowerState {Known=true,Level=73,Connected=false};
                    shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanNext=true,CanPrevious=true,CanSeek=true,Duration=210,Position=62,Title="바탕화면 미디어",Artist="테스트 아티스트",Artwork=ArtworkPalette.Sample(Colors.SteelBlue)};
                    int index=0,commits=0;
                    foreach(string key in new[]{"clock","battery","media","mute","wifi","numbers","buds","devices","windows","capture","manager","clock-analog:2x2","player:4x1","care:2x2","todo:2x2"})
                    {
                        var initial=DesktopWidgetCatalog.Initial(key);
                        var slot=new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key=key,Width=initial.Width,Height=initial.Height};
                        var view=new DesktopWidgetView(shell,slot,(s,a,b,r,c)=>{if(c)commits++;});
                        var host=new DesktopWidgetHost(parent,view);
                        hosts.Add(host);
                        host.Place(new Rect(outer.Left+24*dpi+(index%3)*280*dpi,outer.Top+50*dpi+(index/3)*160*dpi,initial.Width*dpi,initial.Height*dpi),dpi);
                        host.Present(true);
                        view.Refresh();
                        view.Editing(true);
                        await Task.Delay(30);
                        view.Editing(false);
                        Require(host.Alive&&DesktopWidgetNative.GetParent(host.Source.Handle)==parent&&(Native.GetWindowLong(host.Source.Handle,-16)&0x40000000)!=0,"widgets are native desktop children, not topmost windows");
                        Require(view.IsVisible,"a child widget has live WPF content");
                        Program.Save(view,(int)initial.Width,(int)initial.Height,Path.Combine(AppLog.Data,"desktop-widget-"+key.Replace(':','-')+".png"));
                        index++;
                    }
                    Require(Native.GetForegroundWindow()==foreground,"adding widgets does not steal foreground activation");
                    var moving=hosts[0];
                    moving.Place(new Rect(outer.Left+36*dpi,outer.Top+75*dpi,300*dpi,170*dpi),dpi);
                    await Task.Delay(30);
                    Native.RectI actual;
                    Native.GetWindowRect(moving.Source.Handle,out actual);
                    Require(Math.Abs(actual.Left-moving.Pixels.X)<3&&Math.Abs(actual.Top-moving.Pixels.Y)<3&&Math.Abs(actual.Right-actual.Left-moving.Pixels.Width)<3,"native child movement and size match monitor pixels");
                    moving.Present(false);
                    Require(!moving.View.IsVisible,"hidden widgets unload visible animation work");
                    moving.Present(true);
                    Require(moving.View.IsVisible,"widgets return after occlusion");
                    checks.Add("native child hosting, actual DPI movement, no activation theft and visibility lifecycle");
                    // A press whose button came up outside the widget never sees a release.
                    var abandoned=hosts[1].View;
                    abandoned.Press(false,new System.Drawing.Point(10,10));
                    await Task.Delay(700);
                    Require(!abandoned.IsMouseCaptured,"a press released outside the widget must not keep the mouse");
                    bool buttonDown=true;
                    abandoned.ButtonDown=()=>buttonDown;
                    abandoned.Press(false,new System.Drawing.Point(10,10));
                    abandoned.Move(new System.Drawing.Point(80,80));
                    buttonDown=false;
                    await Task.Delay(200);
                    Require(!abandoned.IsMouseCaptured&&abandoned.Opacity==1&&commits==1,"a move whose mouse-up went missing still ends and commits");
                    buttonDown=true;
                    abandoned.Press(false,new System.Drawing.Point(10,10));
                    abandoned.Move(new System.Drawing.Point(80,80));
                    bool lostCaptureChecked=abandoned.IsMouseCaptured;
                    if(lostCaptureChecked)
                    {
                        abandoned.ReleaseMouseCapture();
                        Require(abandoned.Opacity==1&&commits==2,"losing the mouse mid-move ends and commits");
                    }
                    else
                    {
                        buttonDown=false;
                        await Task.Delay(200);
                    }
                    System.Diagnostics.ProcessStartInfo started=null;
                    ElevatedWindows.StartDispatch=info=>started=info;
                    ElevatedWindows.ShowTaskManager();
                    Require(started!=null&&started.FileName.EndsWith("Taskmgr.exe",StringComparison.OrdinalIgnoreCase)&&started.Verb=="","Task Manager is shown by starting it, with no permission prompt");
                    var request=ElevatedWindows.CloseRequest(1234);
                    Require(request.Verb=="runas"&&request.Arguments=="/PID 1234"&&Path.IsPathRooted(request.FileName),"closing an administrator window asks Windows for permission, for that process only");
                    Require(!ElevatedWindows.IsTaskManager(parent),"ordinary windows do not use the Task Manager path");
                    ElevatedWindows.StartDispatch=null;
                    checks.Add("presses and moves that lose their mouse-up release the mouse; capture-loss path "+(lostCaptureChecked?"checked":"not reached"));
                    int chosen=0;
                    var gestureSlot=new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key="calendar:2x2",Width=190,Height=190};
                    var gestureView=new DesktopWidgetView(shell,gestureSlot,(s,a,b,r,c)=>{},s=>chosen++);
                    gestureView.ButtonDown=()=>true;
                    var at=new System.Drawing.Point(10,10);
                    gestureView.Press(false,at);
                    Require(gestureView.Release()&&chosen==1,"clicking an empty part of a widget opens editing");
                    gestureView.Press(true,at);
                    Require(!gestureView.Release()&&chosen==1,"clicking a control keeps the control's action");
                    gestureView.Press(true,at);
                    await Task.Delay(600);
                    Require(chosen==2,"holding a control opens editing");
                    Require(gestureView.Release()&&chosen==2,"releasing a long press does not click or select again");
                    gestureView.Retire();
                    Require(DesktopWidgets.NearestSpan(InfoTileCatalog.Family("gauges:2x1"),4.5)=="gauges:4x1","dragging wider picks the wide tile");
                    Require(DesktopWidgets.NearestSpan(InfoTileCatalog.Family("gauges:2x1"),1.05)=="gauges:2x2","dragging square picks the square tile");
                    Require(DesktopWidgets.NearestSpan(InfoTileCatalog.Family("calendar:2x2"),5)=="calendar:2x2","a one-size tile keeps its shape");
                    var liveSlot=shell.Widgets.Store.Add("clock-digital:2x2","fixture-a",new Size(1200,900));
                    Preferences.Current.DesktopWidgets=true;
                    shell.Widgets.Select(liveSlot);
                    Require(shell.Widgets.Editing&&shell.Widgets.SelectedId==liveSlot.Id,"selecting edits one widget");
                    var away=new Point(-99999,-99999);
                    IntPtr current=Native.GetForegroundWindow();
                    shell.Widgets.CheckSelection(false,away,current);
                    Require(shell.Widgets.Editing,"no click elsewhere keeps editing");
                    shell.Widgets.CheckSelection(true,away,current);
                    Require(!shell.Widgets.Editing,"a click elsewhere ends editing, even one shorter than the timer");
                    shell.Widgets.Select(liveSlot);
                    shell.Widgets.CheckSelection(false,away,parent);
                    Require(shell.Widgets.Editing,"this app's own window does not end editing");
                    IntPtr tray=FindWindow("Shell_TrayWnd",null);
                    if(tray!=IntPtr.Zero)
                    {
                        shell.Widgets.CheckSelection(false,away,tray);
                        Require(!shell.Widgets.Editing,"switching to another app ends editing and closes the toolbar");
                    }
                    shell.Widgets.Deselect();
                    Require(!shell.Widgets.Editing,"editing ends cleanly");
                    Preferences.Current.DesktopWidgets=false;
                    shell.Widgets.Store.Items.Remove(liveSlot);
                    var flat=new DrawingBrush(new GeometryDrawing(Brushes.SteelBlue,null,new RectangleGeometry(new Rect(0,0,100,100))));
                    flat.Freeze();
                    var blurred=WidgetMaterial.Blurred(flat,new Rect(0,0,1600,1000),new Rect(300,200,380,176),20);
                    Require(blurred!=null&&blurred.IsFrozen,"a blurred widget backdrop is one frozen image");
                    Require(WidgetMaterial.Blurred(null,new Rect(0,0,1600,1000),new Rect(300,200,380,176),20)==null&&WidgetMaterial.Blurred(flat,new Rect(0,0,1600,1000),new Rect(300,200,380,176),0)==null,"no wallpaper or zero blur draws nothing");
                    Require(new PreferenceData().WidgetOpacity==100&&new PreferenceData().WidgetBlur==0,"opacity and blur are off by default");
                    Require(Preferences.Normalize(new PreferenceData {WidgetOpacity=-5,WidgetBlur=999}).WidgetOpacity==10&&Preferences.Normalize(new PreferenceData {WidgetBlur=999}).WidgetBlur==40,"material settings are bounded");
                    Require(((SolidColorBrush)WidgetMaterial.Surface(TileScheme.Light,40)).Color.A==102,"opacity sets the card alpha");
                    checks.Add("click, long press and control gestures; aspect-based size snapping; single selection");
                    var handles=hosts.Select(h=>h.Source.Handle).ToArray();
                    foreach(var host in hosts)host.Dispose();
                    hosts.Clear();
                    await Task.Delay(30);
                    Require(handles.All(h=>!DesktopWidgetNative.IsWindow(h)),"closing widgets releases every child handle");
                    Require(shell.Widgets.HostCount==0,"disabled mode creates no desktop hosts");
                    var gallery=new DesktopWidgetGallery(shell.Widgets);
                    gallery.Show();
                    await Task.Delay(80);
                    Program.Save(gallery.Surface,540,680,Path.Combine(AppLog.Data,"desktop-widgets-gallery.png"));
                    gallery.Close();
                    AppLog.Json("desktop-widgets-probe.json",new {passed=true,checks=checks.ToArray(),dpi=dpi,widgetKinds=index,realExplorerMutated=false,systemDevicesChanged=false});
                    result=0;
                }
                catch(Exception e)
                {
                    AppLog.Json("desktop-widgets-probe.json",new {passed=false,error=e.ToString()});
                }
                finally
                {
                    foreach(var host in hosts)host.Dispose();
                    if(owner!=null)owner.Close();
                    if(shell!=null)shell.Dispose();
                    app.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern IntPtr FindWindow(string kind,string name);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Native.PointI point);
        // Live diagnostic for the real Explorer desktop. One temporary child window
        // appears briefly and is released; saved widgets and settings are untouched.
        internal static int Live()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            ShellController shell=null;
            DesktopWidgetHost host=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    IntPtr desktop=DesktopWidgetNative.FindDesktop();
                    Require(DesktopWidgetNative.Available(desktop),"Windows desktop view was not found");
                    shell=new ShellController(true);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    double scale=Native.DropMonitorScale(screen.Bounds);
                    var slot=new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key="clock",X=48,Y=48,Width=260,Height=150};
                    host=new DesktopWidgetHost(desktop,new DesktopWidgetView(shell,slot,(s,a,b,r,c)=>{}));
                    host.Place(DesktopWidgets.Physical(slot,screen,scale),scale);
                    host.Present(true);
                    host.View.Refresh();
                    await Task.Delay(1500);
                    Native.RectI actual;
                    Native.GetWindowRect(host.Source.Handle,out actual);
                    var center=new Native.PointI {X=(actual.Left+actual.Right)/2,Y=(actual.Top+actual.Bottom)/2};
                    IntPtr atCenter=WindowFromPoint(center);
                    bool parented=DesktopWidgetNative.GetParent(host.Source.Handle)==desktop;
                    bool visible=IsWindowVisible(host.Source.Handle);
                    // Unlike WindowFromPoint this ignores other apps' windows covering the desktop.
                    bool receivesClicks=DesktopWidgetNative.ChildAt(desktop,center.X,center.Y)==host.Source.Handle;
                    bool iconsOnTop=DesktopWidgetNative.IconsOnTop(desktop);
                    var iconClock=System.Diagnostics.Stopwatch.StartNew();
                    var icons=await BackgroundWork.Run(()=>DesktopIcons.Read(desktop));
                    long iconMilliseconds=iconClock.ElapsedMilliseconds;
                    bool reached=atCenter==host.Source.Handle;
                    // The real manager on the real desktop, with isolated data: nothing saved by the user is touched.
                    host.Dispose();
                    host=null;
                    Preferences.Current.DesktopWidgets=true;
                    var manager=shell.Widgets;
                    manager.Add("gauges:2x1",screen);
                    var widget=manager.Store.Items[manager.Store.Items.Count-1];
                    await Task.Delay(1500);
                    manager.Pulse();
                    bool selected=manager.SelectedId==widget.Id;
                    bool toolbarOpen=manager.ToolbarOpen;
                    var start=new System.Drawing.Point(800,600);
                    double beforeX=widget.X,beforeY=widget.Y;
                    manager.Drag(widget,start,new System.Drawing.Point(start.X+300,start.Y+180),false,false);
                    manager.Drag(widget,start,new System.Drawing.Point(start.X+300,start.Y+180),false,true);
                    bool moved=widget.X!=beforeX||widget.Y!=beforeY;
                    bool onGrid=widget.X%DesktopWidgetStore.GridStep==0&&widget.Y%DesktopWidgetStore.GridStep==0;
                    await Task.Delay(1200);
                    var pixels=DesktopWidgets.Physical(widget,screen,scale);
                    bool clearOfIcons=!icons.Any(r=>r.IntersectsWith(pixels));
                    manager.Drag(widget,start,start,true,false);
                    manager.Drag(widget,start,new System.Drawing.Point(start.X+(int)(220*scale),start.Y),true,true);
                    await Task.Delay(300);
                    string resized=widget.Key;
                    int rendersBefore=manager.MaterialRenders;
                    Preferences.Current.WidgetOpacity=70;
                    Preferences.Current.WidgetBlur=24;
                    manager.ApplyMaterial();
                    await Task.Delay(1500);
                    manager.Pulse();
                    bool rendered=manager.MaterialRenders>rendersBefore;
                    string image=null;
                    var shot=DesktopWidgets.Physical(widget,screen,scale);
                    var shotCenter=new Native.PointI {X=(int)(shot.X+shot.Width/2),Y=(int)(shot.Y+shot.Height/2)};
                    if(manager.HostCount==1&&DesktopWidgetNative.GetParent(WindowFromPoint(shotCenter))==desktop)
                    {
                        using(var bitmap=new System.Drawing.Bitmap((int)shot.Width,(int)shot.Height))
                        using(var graphics=System.Drawing.Graphics.FromImage(bitmap))
                        {
                            graphics.CopyFromScreen((int)shot.X,(int)shot.Y,0,0,bitmap.Size);
                            image=Path.Combine(AppLog.Data,"desktop-widget-blur-live.png");
                            bitmap.Save(image);
                        }
                    }
                    manager.Deselect();
                    bool ended=!manager.Editing&&!manager.ToolbarOpen;
                    manager.Remove(widget);
                    bool removed=manager.HostCount==0;
                    Preferences.Current.DesktopWidgets=false;
                    bool scenario=selected&&toolbarOpen&&moved&&onGrid&&clearOfIcons&&resized=="gauges:4x1"&&rendered&&ended&&removed;
                    AppLog.Json("desktop-widgets-live.json",new {passed=parented&&visible&&receivesClicks&&!iconsOnTop&&scenario,selected=selected,toolbarOpen=toolbarOpen,moved=moved,onGrid=onGrid,clearOfIcons=clearOfIcons,resized=resized,materialRendered=rendered,editingEnded=ended,removed=removed,screenshot=image!=null,receivesClicks=receivesClicks,iconsOnTop=iconsOnTop,desktopIcons=icons.Count,iconMilliseconds=iconMilliseconds,parented=parented,visible=visible,hitTestReachesWidget=reached,left=actual.Left,top=actual.Top,width=actual.Right-actual.Left,height=actual.Bottom-actual.Top,scale=scale});
                    result=parented&&visible&&receivesClicks&&!iconsOnTop&&scenario?0:1;
                }
                catch(Exception e)
                {
                    AppLog.Json("desktop-widgets-live.json",new {passed=false,error=e.ToString()});
                }
                finally
                {
                    if(host!=null)host.Dispose();
                    if(shell!=null)shell.Dispose();
                    app.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
}
