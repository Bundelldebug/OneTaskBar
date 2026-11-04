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
                    int index=0;
                    foreach(string key in new[]{"clock","battery","media","mute","wifi","numbers","buds","devices","windows","capture","manager","clock-analog:2x2","player:4x1","care:2x2","todo:2x2"})
                    {
                        var initial=DesktopWidgetCatalog.Initial(key);
                        var slot=new DesktopWidgetSlot {Id=Guid.NewGuid().ToString("N"),Key=key,Width=initial.Width,Height=initial.Height};
                        var view=new DesktopWidgetView(shell,slot,s=>{},(s,a,b,r,c)=>{});
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
                    host=new DesktopWidgetHost(desktop,new DesktopWidgetView(shell,slot,s=>{},(s,a,b,r,c)=>{}));
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
                    var icons=await BackgroundWork.Run(()=>DesktopIcons.Read(desktop));
                    AppLog.Json("desktop-widgets-live.json",new {passed=parented&&visible,desktopIcons=icons.Count,parented=parented,visible=visible,hitTestReachesWidget=atCenter==host.Source.Handle,left=actual.Left,top=actual.Top,width=actual.Right-actual.Left,height=actual.Bottom-actual.Top,scale=scale});
                    result=parented&&visible?0:1;
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
