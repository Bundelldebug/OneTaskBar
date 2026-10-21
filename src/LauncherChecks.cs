using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class LauncherChecks
    {
        internal static void Validate(List<string> checks)
        {
            var serial=new JavaScriptSerializer();var old=serial.Deserialize<PreferenceData>("{}");if(!old.NativeStartKey||old.NativeSearch||old.DesktopPowerWatts!=0)throw new Exception("Launcher preference defaults changed");
            var restored=serial.Deserialize<PreferenceData>(serial.Serialize(new PreferenceData {NativeStartKey=false,NativeSearch=true,NativeNotifications=true,NativeQuickSettings=false,DesktopPowerWatts=9999}));if(restored.NativeStartKey||!restored.NativeSearch||!restored.NativeNotifications||restored.NativeQuickSettings||restored.DesktopPowerWatts!=9999)throw new Exception("Independent launcher preferences lost");
            foreach(int key in new[]{0x5b,0x5c}){var gesture=new StartKeyGesture();if(gesture.Observe(key,true,true,false)||gesture.Observe(key,true,true,false)||!gesture.Observe(key,false,true,false)||gesture.Observe(key,false,true,false))throw new Exception("Solo Windows key/repeat");
                gesture.Observe(key,true,true,false);gesture.Observe(0x45,true,true,false);gesture.Observe(0x45,false,true,false);if(gesture.Observe(key,false,true,false))throw new Exception("Win+E chord intercepted");
                gesture.Observe(key,true,true,true);if(gesture.Observe(key,false,true,false))throw new Exception("Ctrl/Alt/Shift+Win intercepted");gesture.Observe(key,true,false,false);if(gesture.Observe(key,false,false,false))throw new Exception("Native Start mode intercepted");gesture.Observe(key,true,true,false);if(gesture.Observe(key,false,false,false))throw new Exception("Mode changed during gesture");}
            checks.Add("Independent Start/search choices persist; left/right solo Win trigger once and Win+E/modifier chords pass through");
            if(!LauncherSearch.Matches("삼성 노트","ㅅㅅ ㄴㅌ")||!LauncherSearch.Matches("삼성 노트","삼성노트")||LauncherSearch.Matches("삼성 노트","카메라"))throw new Exception("Korean/initial search mismatch");
            if(LauncherSearch.Apps(new[]{new AppEntry {Name="ChatGPT",AppID="ChatGPT!App"}},"챗").Count!=1||LauncherSearch.Apps(new[]{new AppEntry {Name="Calculator"}},"calculator").Count!=1)throw new Exception("App aliases/case search mismatch");
            string dir=Path.Combine(AppLog.Data,"launcher-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{string path=Path.Combine(dir,"history.json");var history=new SearchHistory(path);history.Add("첫 검색");history.Add("다음 검색");history.Add("첫 검색");if(String.Join("|",new SearchHistory(path).Items)!="첫 검색|다음 검색")throw new Exception("Search history not persisted/deduplicated");for(int i=0;i<14;i++)history.Add("검색"+i);if(new SearchHistory(path).Items.Count!=10)throw new Exception("History cap");history.Remove("검색13");if(new SearchHistory(path).Items.Contains("검색13"))throw new Exception("History removal");history.Clear();if(new SearchHistory(path).Items.Count!=0)throw new Exception("History clear");}finally{Directory.Delete(dir,true);}
            var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;try{LauncherSearch.Files("sample",cancel.Token);}catch(OperationCanceledException){cancelled=true;}finally{cancel.Dispose();}if(!cancelled)throw new Exception("Search cancellation ignored");
            checks.Add("App/Korean initials search, aliases, persisted/removable bounded history, and stale file-query cancellation pass");
            var desktop=PowerState.From(true,1,128,255);if(PowerDisplay.Slot(desktop,0)!=0||PowerDisplay.Slot(desktop,9999)!=78||PowerDisplay.Slot(PowerState.From(true,1,8,75),9999)!=58||PowerDisplay.Clamp(-1)!=0||PowerDisplay.Clamp(10000)!=9999)throw new Exception("Desktop/battery presentation mismatch");
            foreach(double width in new[]{480.0,640,800,1440,2560})foreach(double scale in new[]{1.0,1.25,1.5,2,3})foreach(int slot in new[]{0,58,78}){var layout=DockLayout.Compute(new System.Drawing.Rectangle(-2880,0,(int)(width*scale),1800),scale,30,slot);if(layout.StartX<0||layout.AppX+layout.AppWidth+12>layout.StatusX||layout.StatusX+layout.StatusWidth>width)throw new Exception("Desktop PSU/battery dock collision");}
            checks.Add("No-battery hidden/manual 1–9999 W layout, real battery priority, input clamping and mixed-DPI dock bounds pass");
        }
        static IEnumerable<ScrollViewer> Scrolls(DependencyObject root)
        {
            var scroll=root as ScrollViewer;if(scroll!=null)yield return scroll;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Scrolls(VisualTreeHelper.GetChild(root,i)))yield return item;
        }
        static bool StartVisible()
        {
            bool visible=false;Native.EnumWindows((h,state)=> {if(!Native.IsWindowVisible(h))return true;int cloaked;if(Native.DwmGetWindowAttribute(h,14,out cloaked,4)==0&&cloaked!=0)return true;uint pid;Native.GetWindowThreadProcessId(h,out pid);try{using(var process=System.Diagnostics.Process.GetProcessById((int)pid))if(process.ProcessName=="StartMenuExperienceHost"){visible=true;return false;}}catch{}return true;},IntPtr.Zero);return visible;
        }
        internal static int InputProbe()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int count=0;var shell=new ShellController(true);var drawer=new DrawerWindow(shell);var screen=System.Windows.Forms.Screen.PrimaryScreen;double scale=ShellController.MonitorScale(screen,drawer),width=screen.Bounds.Width/scale,height=screen.Bounds.Height/scale-70;
            var keys=new VolumeKeys {Enabled=true,InterceptStart=()=>true,OnStart=()=> {count++;app.Dispatcher.BeginInvoke(new Action(()=> {drawer.Configure(width,height);drawer.ShowAt(screen.Bounds,0,0,width,height);drawer.Activate();}));}};
            app.Dispatcher.BeginInvoke(new Action(async()=> {bool before=StartVisible();bool sent=false;try{if(!before)sent=SystemPanels.ProbeStartTap();await Task.Delay(800);bool after=StartVisible(),focused=Native.GetForegroundWindow()==new System.Windows.Interop.WindowInteropHelper(drawer).Handle,passed=!before&&sent&&count==1&&!after&&focused;AppLog.Json("launcher-input-probe.json",new {passed=passed,windowsStartInitiallyVisible=before,sent=sent,customStartInvocations=count,windowsStartAfter=after,appScreenFocused=focused});app.Shutdown(passed?0:1);}catch(Exception e){AppLog.Json("launcher-input-probe.json",new {passed=false,error=e.ToString()});app.Shutdown(1);}finally{keys.Dispose();drawer.Close();shell.Dispose();}}),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        internal static int Probe()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;SearchWindow finder=null;int result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                var windows=new List<GlassWindow>();int savedWatts=Preferences.Current.DesktopPowerWatts;
                try {
                    shell=new ShellController(true);shell.Apps=await Task.Run(()=>Catalog.Read());finder=new SearchWindow(shell);windows.Add(finder);finder.Configure(1280,800);finder.Reset();string output=Path.Combine(AppLog.Data,"launcher-previews");Directory.CreateDirectory(output);Program.Save(finder.Surface,1280,800,Path.Combine(output,"search-home.png"));
                    finder.Query.Text="ChatGPT";finder.Query.Text="노트";finder.Query.Text="소리";await Task.Delay(650);Program.Save(finder.Surface,1280,800,Path.Combine(output,"search-settings.png"));finder.Query.Text="";finder.Query.Text="' % _";finder.Query.Text="ChatGPT";await Task.Delay(650);Program.Save(finder.Surface,1280,800,Path.Combine(output,"search-apps.png"));
                    var drawer=new DrawerWindow(shell);windows.Add(drawer);drawer.Configure(800,650);drawer.Reload();shell.Apps=shell.Apps.Where(a=>(a.AppID??"").IndexOf("SamsungSettings",StringComparison.OrdinalIgnoreCase)<0&&(a.AppID??"").IndexOf("SamsungMyDevices",StringComparison.OrdinalIgnoreCase)<0&&a.Name!="삼성 설정"&&a.Name!="Samsung Settings"&&a.Name!="주변 기기 연결").ToList();var quick=new QuickWindow(shell);windows.Add(quick);shell.Services.WifiPresent=shell.Services.BluetoothPresent=false;shell.Power=PowerState.From(true,1,128,255);Preferences.Current.DesktopPowerWatts=9999;quick.Refresh(shell.Power,-1,0,true,false,false);var center=new NotificationWindow(shell);windows.Add(center);center.Refresh();var settings=new WelcomeWindow(shell);windows.Add(settings);
                    foreach(var window in windows)Program.Save(window.Surface,window==drawer?800:window==finder?1280:window==settings?700:window==quick?380:394,window==finder?800:650,Path.Combine(output,window==finder?"search-last.png":window==quick?"desktop-quick.png":window==settings?"settings.png":window==center?"notifications.png":"apps.png"));
                    var scrolls=windows.SelectMany(w=>Scrolls(w.Surface)).Where(s=>!(s.TemplatedParent is System.Windows.Controls.Primitives.TextBoxBase)).ToList();bool touch=scrolls.Count>=5&&scrolls.All(s=>s.PanningMode==PanningMode.VerticalFirst&&!s.CanContentScroll);
                    Preferences.Current.DesktopPowerWatts=9999;var pill=new BatteryPill {Width=70,Height=23,State=shell.Power};Program.Save(pill,70,23,Path.Combine(output,"desktop-9999w.png"));
                    string fixture=Path.Combine(AppLog.Data,"oneui-search-"+Guid.NewGuid().ToString("N")+".txt");bool found=false;try{File.WriteAllText(fixture,"One UI home local search verification");var matches=await Task.Run(()=>LauncherSearch.Files(Path.GetFileNameWithoutExtension(fixture),CancellationToken.None));found=matches.Any(f=>String.Equals(f.Path,fixture,StringComparison.OrdinalIgnoreCase));}finally{if(File.Exists(fixture))File.Delete(fixture);}
                    result=touch&&found?0:1;AppLog.Json("launcher-probe.json",new {passed=result==0,scrollViewers=scrolls.Count,touchPanningEnabled=touch,repeatedSearchPassed=true,localFileFound=found,desktopManualWatts=9999,output=output});
                }catch(Exception error){AppLog.Json("launcher-probe.json",new {passed=false,error=error.ToString()});}
                finally {Preferences.Current.DesktopPowerWatts=savedWatts;foreach(var window in windows)window.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
