using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class ReferenceChecks
    {
        static async Task FocusFixture(Window window)
        {
            SystemPanels.CloseSearchProbe();var handle=new WindowInteropHelper(window).Handle;Native.ShowWindow(handle,5);Native.Activate(handle);await Task.Delay(80);if(Native.GetForegroundWindow()==handle)return;
            window.Topmost=true;Native.SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x40);await Task.Delay(100);var point=window.PointToScreen(new Point(45,75));if(Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)point.X,Y=(int)point.Y}),2)!=handle)throw new Exception("Focus fixture point is obscured; no input sent: "+PointInfo(point,handle));SetCursorPos((int)point.X,(int)point.Y);mouse_event(2,0,0,0,UIntPtr.Zero);mouse_event(4,0,0,0,UIntPtr.Zero);await Task.Delay(100);window.Topmost=false;
        }
        static string PointInfo(Point p,IntPtr own){var hit=WindowFromPoint(new Native.PointI {X=(int)p.X,Y=(int)p.Y});var text=new System.Text.StringBuilder(128);Native.GetClassName(hit,text,128);uint pid;Native.GetWindowThreadProcessId(hit,out pid);Native.RectI bounds;Native.GetWindowRect(own,out bounds);int cloaked;Native.DwmGetWindowAttribute(own,14,out cloaked,4);string z="";Native.EnumWindows((h,l)=>{Native.RectI r;if(Native.IsWindowVisible(h)&&Native.GetWindowRect(h,out r)&&p.X>=r.Left&&p.X<r.Right&&p.Y>=r.Top&&p.Y<r.Bottom){var c=new System.Text.StringBuilder(96);Native.GetClassName(h,c,96);if(z.Length<600)z+=h+":"+c+":"+Native.GetWindowLong(h,-20)+";";}return true;},IntPtr.Zero);return "z="+z+", ownStyle="+Native.GetWindowLong(own,-20)+", visible="+Native.IsWindowVisible(own)+", cloaked="+cloaked+", own="+own+", point="+p+", hit="+hit+", root="+Native.GetAncestor(hit,2)+", class="+text+", pid="+pid+", bounds="+bounds.Left+","+bounds.Top+","+bounds.Right+","+bounds.Bottom+", fg="+FocusName();}
        internal static string FocusName(){uint pid;Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid);try{using(var process=System.Diagnostics.Process.GetProcessById((int)pid))return process.ProcessName;}catch{return "unknown";}}
        [DllImport("user32.dll")]static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Native.PointI point);
        [DllImport("user32.dll")]static extern void mouse_event(uint flags,uint x,uint y,uint data,UIntPtr extra);
        static IEnumerable<T> Children<T>(DependencyObject root)where T:DependencyObject {var match=root as T;if(match!=null)yield return match;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Children<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;DockHost dock=null;var first=new Window {Title="One TaskBar test A",Background=Brushes.White,Content="Activation fixture A",Width=380,Height=180,Left=100,Top=180};var other=new Window {Title="One TaskBar test B",Background=Brushes.White,Content="Activation fixture B",Width=380,Height=180,Left=500,Top=180};bool buttonChoice=Preferences.Current.NativeStartButton,keyChoice=Preferences.Current.NativeStartKey;var cursor=System.Windows.Forms.Cursor.Position;VolumeKeys keys=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Program.Inspectable=true;shell=new ShellController(true);first.Show();other.Show();var firstHandle=new WindowInteropHelper(first).Handle;var otherHandle=new WindowInteropHelper(other).Handle;var entry=new AppEntry {Name="Activation fixture",Target="fixture"};entry.Windows.Add(firstHandle);shell.Pins.Add(entry);dock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);dock.Apps.ShowAt(dock.Screen.Bounds,30,30,154,44);dock.Apps.UpdateLayout();var button=((Canvas)dock.Apps.Surface.Child).Children.OfType<Button>().Last();var icon=Children<Glyph>(button).First();
                    await FocusFixture(first);shell.LastApplication=otherHandle;await Task.Delay(120);var point=button.PointToScreen(new Point(button.ActualWidth/2,button.ActualHeight/2));if(Native.GetForegroundWindow()!=firstHandle||Native.GetAncestor(WindowFromPoint(new Native.PointI {X=(int)point.X,Y=(int)point.Y}),2)!=new WindowInteropHelper(dock.Apps).Handle)throw new Exception("Fixture not focused or dock point is not owned; no mouse input sent");
                    SetCursorPos((int)point.X,(int)point.Y);await Task.Delay(50);mouse_event(2,0,0,0,UIntPtr.Zero);await Task.Delay(30);mouse_event(4,0,0,0,UIntPtr.Zero);double peak=1;bool contained=true;for(int n=0;n<22;n++){await Task.Delay(18);var bounds=icon.TransformToAncestor(dock.Apps.Surface).TransformBounds(new Rect(0,0,icon.ActualWidth,icon.ActualHeight));contained&=bounds.Top>=-.5&&bounds.Bottom<=dock.Apps.Surface.ActualHeight+.5;peak=Math.Max(peak,((ScaleTransform)((TransformGroup)icon.RenderTransform).Children[0]).ScaleX);}
                    bool minimized=Native.IsIconic(firstHandle);Native.Activate(otherHandle);await Task.Delay(60);shell.ActivateApp(entry,button);await Task.Delay(100);bool restored=!Native.IsIconic(firstHandle)&&Native.GetForegroundWindow()==firstHandle;
                    entry.Windows.Add(otherHandle);Native.Activate(firstHandle);await Task.Delay(60);shell.ActivateApp(entry,button);await Task.Delay(100);bool group=Native.IsIconic(firstHandle)&&!Native.IsIconic(otherHandle);
                    Preferences.Current.NativeStartButton=false;dock.Rebuild();shell.ToggleStartButton(dock);await Task.Delay(280);bool custom=shell.StartMenu!=null&&shell.StartMenu.IsVisible;shell.StartMenu.HideImmediately();
                    int intercepted=0;keys=new VolumeKeys {Enabled=true,InterceptStart=()=>true,OnStart=()=>intercepted++};Preferences.Current.NativeStartButton=true;Preferences.Current.NativeStartKey=false;dock.Rebuild();dock.Start.ShowAt(dock.Screen.Bounds,240,30,40,40);dock.Status.ShowAt(dock.Screen.Bounds,300,30,282,44);dock.Start.UpdateLayout();dock.Status.UpdateLayout();bool logo=Children<Glyph>(dock.Start.Surface).Any(g=>g.Kind=="windows-start");Native.Activate(otherHandle);await Task.Delay(80);shell.ToggleStartButton(dock);bool native=false;for(int i=0;i<25&&!native;i++){await Task.Delay(80);native=LauncherChecks.StartVisible();}var nativeTitle=new System.Text.StringBuilder(120);Native.GetWindowText(Native.GetForegroundWindow(),nativeTitle,120);string focus=FocusName()+" / "+nativeTitle.ToString();if(native)SystemPanels.Start();
                    var wifi=Native.ReadWifi();shell.Wifi="Fixture";shell.WifiGeneration=6;shell.Services.WifiOn=true;dock.Refresh(new PowerState());bool badge=Children<Glyph>(dock.Status.Surface).Any(g=>g.Kind=="wifi"&&g.Generation==6);shell.Services.WifiOn=false;dock.Refresh(new PowerState());bool off=Children<Glyph>(dock.Status.Surface).Any(g=>g.Kind=="wifi-off"&&g.Generation==0);
                    result=minimized&&restored&&group&&custom&&native&&logo&&intercepted==0&&badge&&off&&peak>1.09&&contained?0:1;AppLog.Json("reference-probe.json",new {passed=result==0,realDockClickMinimized=minimized,restoredOnRepeat=restored,activeGroupMinimized=group,customButton=custom,nativeButton=native,nativeFocus=focus,windowsLogo=logo,nativeBypassedCustomKeyboardHook=intercepted==0,wifiBadge=badge,badgeHiddenWhenOff=off,actualWifiConnected=wifi.Name!=null,actualWifiGeneration=wifi.Generation,bouncePeak=peak,bounceInsideCapsule=contained});
                }catch(Exception error){AppLog.Json("reference-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(keys!=null)keys.Dispose();if(FocusName()=="SearchHost")SystemPanels.CloseSearchProbe();Preferences.Current.NativeStartButton=buttonChoice;Preferences.Current.NativeStartKey=keyChoice;SetCursorPos(cursor.X,cursor.Y);if(dock!=null)dock.Close();first.Close();other.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
