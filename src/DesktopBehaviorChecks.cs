using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace OneTaskBar
{
    internal static class DesktopBehaviorChecks
    {
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        internal static void Validate(List<string> checks)
        {
            int count=0;foreach(var screen in new[]{new System.Drawing.Rectangle(0,0,3840,2160),new System.Drawing.Rectangle(-1440,-700,1440,2560),new System.Drawing.Rectangle(3840,900,1920,1080)})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5}){
                var layout=DockLayout.Compute(screen,scale,8);var menu=Native.PlacementBounds(screen,scale,layout.StartX,layout.Y+2,40,40);var apps=Native.PlacementBounds(screen,scale,layout.AppX,layout.Y,layout.AppWidth,44);if(menu.Left<screen.Left||menu.Top<screen.Top||menu.Right>apps.Left||menu.Bottom>screen.Bottom||apps.Right>screen.Right)throw new Exception("Mixed-DPI/negative-origin dock placement");
                int native=screen.Bottom-(int)Math.Round(48*scale),top=AppBarReservation.ReservationTop(screen,scale,native,false);if(top!=screen.Bottom-(int)Math.Round(68*scale)||AppBarReservation.ReservationTop(screen,scale,native,true)!=native||AppBarReservation.ReservationTop(screen,scale,screen.Bottom-(int)(100*scale),false)!=screen.Bottom-(int)(100*scale))throw new Exception("Autohide or existing taskbar reservation doubled");
                foreach(double reserved in new[]{68.0,72,96,100}){double height=screen.Height/scale,y=DockLayout.Compute(screen,scale,8,58,false,reserved).Y;if(Math.Abs(y-(height-reserved)-(height-y-44))>.01)throw new Exception("Dock gaps must match the actual work-area reservation");}count++;
            }
            var full=new Native.RectI {Left=0,Top=0,Right=1920,Bottom=1080};var area=new System.Drawing.Rectangle(0,0,1920,1080);if(!Native.CoversMonitor(full,area,false,false)||Native.CoversMonitor(full,area,true,true)||!Native.CoversMonitor(full,area,true,false))throw new Exception("Maximized window misclassified as fullscreen");
            foreach(string kind in new[]{"VirtualDisplaySurface","HwndWrapper[mirror]","Windows.UI.Core.CoreWindow"}){
                if(!FullscreenVisibility.PassiveSurface(0x08000080,false,kind,true)||!FullscreenVisibility.PassiveSurface(0xA0,false,kind,false)||FullscreenVisibility.PassiveSurface(0x80,true,kind,true)||FullscreenVisibility.PassiveSurface(0,false,kind,true))throw new Exception("Virtual-display passive surface / active fullscreen classification");
            }
            var json=new JavaScriptSerializer();if(json.Deserialize<DesktopLease>("{\"Active\":true,\"TaskbarState\":1}").RestoreTaskbarState==false||json.Deserialize<DesktopLease>(json.Serialize(new DesktopLease {RestoreTaskbarState=false})).RestoreTaskbarState!=false)throw new Exception("Legacy recovery compatibility / native setting preservation");
            var temporary=json.Deserialize<DesktopLease>(json.Serialize(new DesktopLease {RestoreTaskbarState=false,TemporaryTaskbarState=1}));if(temporary.TemporaryTaskbarState!=1)throw new Exception("Temporary tray state lost its crash recovery snapshot");
            checks.Add("Three-monitor landscape/portrait/negative-origin placement at six DPI scales; autohide work-area return, no double reservation, maximized-window classification and legacy recovery compatibility pass ("+count+" layouts)");
        }
        static bool Bounds(Window window,Native.RectI expected){Native.RectI actual;return Native.GetWindowRect(new WindowInteropHelper(window).Handle,out actual)&&Math.Abs(actual.Left-expected.Left)<=1&&Math.Abs(actual.Top-expected.Top)<=1&&Math.Abs(actual.Right-expected.Right)<=1&&Math.Abs(actual.Bottom-expected.Bottom)<=1;}
        static bool Contents(DockHost dock){return new[]{dock.Start,dock.Apps,dock.Status}.All(w=>w.IsVisible&&w.Surface.Opacity==1&&((FrameworkElement)w.Surface.Child).Opacity==1&&((FrameworkElement)w.Surface.Child).RenderTransform.Value.IsIdentity&&w.Surface.IsHitTestVisible);}
        internal static int NativeState()
        {
            bool original=Native.TaskbarAutoHide(),restored=false;string error=null;var samples=new List<object>();
            try{foreach(bool value in new[]{true,false,true,false}){Native.SetTaskbarAutoHide(value);for(int i=0;i<20&&Native.TaskbarAutoHide()!=value;i++)System.Threading.Thread.Sleep(50);bool read=Native.TaskbarAutoHide();samples.Add(new {requested=value,actual=read});if(read!=value)throw new Exception("Native taskbar state did not follow the requested value");}}
            catch(Exception failure){error=failure.ToString();}finally{Native.SetTaskbarAutoHide(original);for(int i=0;i<20&&Native.TaskbarAutoHide()!=original;i++)System.Threading.Thread.Sleep(50);restored=Native.TaskbarAutoHide()==original;}
            bool passed=error==null&&restored;AppLog.Json("taskbar-state-probe.json",new {passed=passed,initiallyAutoHiddenThenDisabled=true,samples=samples.ToArray(),originalWindowsChoiceRestored=restored,error=error});return passed?0:1;
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;DockHost dock=null;GlassWindow legacy=null,menu=null;var monitors=new List<object>();IDisposable motion=null;bool oldAnimations=Preferences.Current.Animations;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try{
                    Program.Inspectable=true;await DockMaterialChecks.Run();motion=Motion.WithAnimationsForFixture();Preferences.Current.Animations=true;Directory.CreateDirectory(AppLog.Data);Validate(new List<string>());shell=new ShellController(true);shell.Desktop=true;shell.Pins=Enumerable.Range(1,4).Select(i=>new AppEntry {Name="검사 앱 "+i,Target="fixture:"+i,Icon=ArtworkPalette.Sample(Colors.CornflowerBlue)}).ToList();var screen=Forms.Screen.PrimaryScreen;
                    legacy=new GlassWindow {ShowActivated=false,Width=200,Height=80};legacy.Surface.Child=Paint.TextBlock("이전 애니메이션 경로",14,Paint.Text);legacy.ShowAt(screen.Bounds,40,40,200,80);legacy.Hide();await Task.Delay(20);legacy.Show();await Task.Delay(260);bool oldFade=((FrameworkElement)legacy.Surface.Child).Opacity<.01;legacy.Close();legacy=null;
                    bool placement=true;foreach(var monitor in Forms.Screen.AllScreens){dock=new DockHost(shell,monitor);dock.Show();await Task.Delay(80);var fit=DockLayout.Compute(monitor.Bounds,dock.Scale,4,PowerDisplay.DockSlot(shell.Power,Preferences.Current.DesktopPowerWatts),false,68,3,Preferences.Current.DuoStatusIcon);bool aligned=Bounds(dock.Start,Native.PlacementBounds(monitor.Bounds,dock.Scale,fit.StartX,fit.Y+2,40,40))&&Bounds(dock.Apps,Native.PlacementBounds(monitor.Bounds,dock.Scale,fit.AppX,fit.Y,fit.AppWidth,44))&&Bounds(dock.Status,Native.PlacementBounds(monitor.Bounds,dock.Scale,fit.StatusX,fit.Y,fit.StatusWidth,44));placement&=aligned;monitors.Add(new {bounds=monitor.Bounds,scale=dock.Scale,aligned=aligned});dock.Close();dock=null;}
                    dock=new DockHost(shell,screen);dock.Show();await Task.Delay(100);bool returned=true;for(int i=0;i<8;i++){dock.SetVisible(false);await Task.Delay(i%3*10);dock.SetVisible(true);}await Task.Delay(250);returned=Contents(dock)&&dock.ShownKeys.Length==4;dock.SetVisible(false);dock.Rebuild();await Task.Delay(220);bool staysHidden=!dock.Start.IsVisible&&!dock.Apps.IsVisible&&!dock.Status.IsVisible;dock.SetVisible(true);
                    bool desktopRecovery=await DesktopReturn(dock);await DockShellChecks.Run(dock);
                    dock.UpdateVisibility(false,true);dock.CompleteAutoHide(true);bool held=dock.DockVisible&&!dock.EdgeVisible;dock.CompleteAutoHide(false);await Task.Delay(240);bool hidden=!dock.DockVisible&&dock.EdgeVisible&&!dock.Apps.IsVisible&&!dock.Status.IsVisible;Native.RectI edge;Native.GetWindowRect(dock.RevealHandle,out edge);bool thin=edge.Bottom-edge.Top==2&&edge.Left==screen.Bounds.Left&&edge.Right==screen.Bounds.Right;bool hit=Native.GetAncestor(WindowFromPoint(new Native.PointI {X=screen.Bounds.Left+20,Y=screen.Bounds.Bottom-1}),2)==dock.RevealHandle;
                    SendMessage(dock.RevealHandle,0x200,IntPtr.Zero,new IntPtr(0x10014));await Task.Delay(35);bool mid=dock.AutoHideAnimating&&dock.RevealProgress>0&&dock.RevealProgress<1;bool blurSync=new[]{dock.Start,dock.Apps,dock.Status}.All(w=>Math.Abs(w.Opacity-Native.BlurOpacity(w))<.002);await Task.Delay(280);bool revealed=Contents(dock)&&dock.DockVisible&&!dock.EdgeVisible&&!dock.AutoHideAnimating&&dock.RevealProgress==1;
                    dock.CompleteAutoHide(false);await Task.Delay(40);dock.Reveal();await Task.Delay(280);bool interrupted=Contents(dock)&&dock.RevealProgress==1&&!dock.AutoHideAnimating;dock.UpdateVisibility(true,true);bool fullscreen=!dock.DockVisible&&!dock.EdgeVisible;dock.UpdateVisibility(false,true);dock.Reveal();dock.UpdateVisibility(false,false);bool disabled=Contents(dock)&&dock.RevealHandle==IntPtr.Zero&&!dock.AutoHideAnimating;
                    var before=Native.WorkArea(screen);bool flags=Native.TaskbarAutoHide();using(var reserve=new AppBarReservation(screen,dock.Scale,true)){await Task.Delay(100);var during=Native.WorkArea(screen);bool noSpace=during.Equals(before);if(!noSpace)throw new Exception("Hidden appbar took additional workspace");}bool unchanged=flags==Native.TaskbarAutoHide();
                    menu=new GlassWindow {ShowActivated=false,Width=620,Height=300};Native.SeedMonitor(menu,screen.Bounds);bool logicalSize=menu.Width==620&&menu.Height==300;bool menuPlacement=true;foreach(var monitor in Forms.Screen.AllScreens.Reverse()){double scale=ShellController.MonitorScale(monitor,menu);double width=Math.Min(720,monitor.Bounds.Width/scale-24);menu.ShowAt(monitor.Bounds,12,12,width,300);await Task.Delay(40);menuPlacement&=Bounds(menu,Native.PlacementBounds(monitor.Bounds,scale,12,12,width,300));}menuPlacement&=logicalSize;
                    bool passed=desktopRecovery&&placement&&menuPlacement&&returned&&staysHidden&&held&&hidden&&thin&&hit&&revealed&&mid&&blurSync&&interrupted&&fullscreen&&disabled&&unchanged;AppLog.Json("desktop-behavior-probe.json",new {passed=passed,desktopReturnRestoresNativeDock=desktopRecovery,legacyFadeRaceReproduced=oldFade,rapidCaptureReturnRestoresIcons=returned,rebuildDoesNotReshowHiddenWindows=staysHidden,actualMonitorPlacement=placement,menuRelocation=menuPlacement,actualMonitorCount=Forms.Screen.AllScreens.Length,monitors=monitors,syntheticThreeMonitorLayouts=18,autoHideHoldsDuringInteraction=held,autoHideHidesAllWindows=hidden,edgeTwoPixels=thin,edgeHitTest=hit,edgeMouseMessageReveals=revealed,revealHasIntermediateFrames=mid,contentAndBlurOpacityMatch=blurSync,interruptedHideReversesSmoothly=interrupted,fullscreenDisablesEdge=fullscreen,disablingAutoHideRestoresDock=disabled,hiddenAppbarAddsNoWorkspace=true,nativeAutoHideSettingUnchanged=unchanged,globalKeyboardShortcutSent=false,externalAppsLaunched=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("desktop-behavior-probe.json",new {passed=false,error=error.ToString()});}finally{if(legacy!=null)legacy.Close();if(menu!=null)menu.Close();if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();if(motion!=null)motion.Dispose();Preferences.Current.Animations=oldAnimations;app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        static async Task<bool> DesktopReturn(DockHost dock)
        {
            bool hover=Preferences.Current.Hover;try{using(Motion.WithAnimationsForFixture()){Preferences.Current.Hover=true;foreach(var button in new[]{dock.SearchButton,dock.TaskViewButton}){var glyph=(FrameworkElement)button.Content;var scale=((TransformGroup)glyph.RenderTransform).Children.OfType<ScaleTransform>().Single();button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseEnterEvent});await Task.Delay(55);if(scale.ScaleX<=1)throw new Exception("Dock utility hover has no intermediate enlargement");button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseLeaveEvent});await Task.Delay(170);if(Math.Abs(scale.ScaleX-1)>.001)throw new Exception("Dock utility hover did not settle");}}}finally{Preferences.Current.Hover=hover;}
            var windows=new[]{dock.Start,dock.Apps,dock.Status};var foreground=Native.GetForegroundWindow();bool mismatch=false,passed=true;
            for(int i=0;i<12;i++){
                foreach(var window in windows){var handle=new WindowInteropHelper(window).Handle;Native.ShowWindow(handle,i%2==0?0:6);var owner=Native.GetWindow(handle,4);if(owner!=IntPtr.Zero)Native.ShowWindow(owner,0);mismatch|=dock.DockVisible&&(!Native.IsWindowVisible(handle)||Native.IsIconic(handle));}
                dock.UpdateActive(IntPtr.Zero);dock.UpdateVisibility(false,false);await Task.Delay(15);
                passed&=Contents(dock)&&windows.All(w=>{var handle=new WindowInteropHelper(w).Handle;var owner=Native.GetWindow(handle,4);return Native.IsWindowVisible(handle)&&!Native.IsIconic(handle)&&(owner==IntPtr.Zero||Native.IsWindowVisible(owner));});
            }
            // A recovery must not override intentional fullscreen or auto-hide hiding.
            dock.UpdateVisibility(true,false);dock.UpdateVisibility(true,false);bool fullHidden=windows.All(w=>!Native.IsWindowVisible(new WindowInteropHelper(w).Handle));dock.UpdateVisibility(false,false);
            return mismatch&&passed&&fullHidden&&Native.GetForegroundWindow()==foreground;
        }
    }
}
