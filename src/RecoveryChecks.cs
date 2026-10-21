using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class RecoveryChecks
    {
        internal static int NativeSearch()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;var window=new Window {Title="One TaskBar · Windows search verification",Width=500,Height=160};
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    window.Show();window.Activate();await Task.Delay(150);bool sent=await Task.Run(()=>SystemPanels.Search("OneTaskBarProbe"));bool found=false;
                    for(int n=0;n<20&&!found;n++){await Task.Delay(100);uint pid;IntPtr handle=Native.GetForegroundWindow();Native.GetWindowThreadProcessId(handle,out pid);using(var process=Process.GetProcessById((int)pid)){if(process.ProcessName!="SearchHost"&&process.ProcessName!="SearchApp"&&process.ProcessName!="SearchUI")break;}
                        var elements=System.Windows.Automation.AutomationElement.FromHandle(handle).FindAll(System.Windows.Automation.TreeScope.Descendants,new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ControlTypeProperty,System.Windows.Automation.ControlType.Edit));foreach(System.Windows.Automation.AutomationElement edit in elements){object pattern;if(edit.TryGetCurrentPattern(System.Windows.Automation.ValuePattern.Pattern,out pattern)&&((System.Windows.Automation.ValuePattern)pattern).Current.Value=="OneTaskBarProbe")found=true;}}
                    code=sent&&found?0:1;AppLog.Json("native-search-probe.json",new {passed=code==0,querySent=sent,queryInWindowsSearch=found});
                }catch(Exception error){AppLog.Json("native-search-probe.json",new {passed=false,error=error.ToString()});}
                finally{SystemPanels.CloseSearchProbe();window.Close();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        internal static int Desktop()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;var window=new Window {Title="One TaskBar · desktop recovery verification",Width=500,Height=180,Content="Verifying taskbar restoration after emergency exit."};Process child=null;string data=Path.Combine(AppLog.Data,"desktop-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                var before=System.Windows.Forms.Screen.AllScreens.Select(s=>Native.WorkArea(s)).ToArray();
                try{
                    File.WriteAllText(Path.Combine(data,"settings.json"),"{\"EmergencyExit\":true,\"NativeNotifications\":true}");window.Show();
                    var start=new ProcessStartInfo(Path.Combine(AppLog.Root,"OneTaskBar.exe"),"--desktop"){UseShellExecute=false,CreateNoWindow=true};start.EnvironmentVariables["ONE_TASKBAR_DATA"]=data;child=Process.Start(start);
                    string ready=Path.Combine(data,"guard-"+child.Id+".ready"),lease=Path.Combine(data,"desktop-lease.json");for(int i=0;i<160&&(!File.Exists(ready)||!File.Exists(lease));i++)await Task.Delay(50);if(!File.Exists(ready)||!File.Exists(lease)||!File.ReadAllText(lease).Contains("\"Active\":true"))throw new Exception("Desktop/watchdog did not start");
                    await Task.Delay(900);bool hidden=TaskbarMask.Protected(Native.FindWindow("Shell_TrayWnd",null));var during=System.Windows.Forms.Screen.AllScreens.Select(s=>Native.WorkArea(s)).ToArray();IntPtr own=new WindowInteropHelper(window).Handle;Native.Activate(own);await Task.Delay(100);if(Native.GetForegroundWindow()!=own)throw new Exception("Recovery input window is not focused");bool sent=SystemPanels.ProbeEmergency();for(int i=0;i<100&&!child.HasExited;i++)await Task.Delay(50);
                    for(int i=0;i<120&&File.ReadAllText(lease).Contains("\"Active\":true");i++)await Task.Delay(50);await Task.Delay(200);var after=System.Windows.Forms.Screen.AllScreens.Select(s=>Native.WorkArea(s)).ToArray();bool restored=before.SequenceEqual(after)&&!File.ReadAllText(lease).Contains("\"Active\":true");
                    code=hidden&&sent&&child.HasExited&&restored?0:1;AppLog.Json("desktop-recovery-probe.json",new {passed=code==0,taskbarHiddenDuring=hidden,forcedExit=child.HasExited,leaseReleased=restored,before=before,during=during,after=after});
                }catch(Exception error){AppLog.Json("desktop-recovery-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(child!=null){try{if(!child.HasExited){Program.Signal("Stop");if(!child.WaitForExit(3000))child.Kill();}}catch{}child.Dispose();}window.Close();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;var window=new Window {Title="One TaskBar · recovery verification",Width=500,Height=180,Content="Testing recovery of an isolated, unresponsive OneTaskBar process."};Process child=null;GuardProcess.Child guard=null;string data=Path.Combine(AppLog.Data,"guard-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(data);
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    window.Show();window.Activate();await Task.Delay(100);string settings=Path.Combine(data,"settings.json");File.WriteAllText(settings,"{\"EmergencyExit\":false}");
                    child=Process.Start(new ProcessStartInfo(Path.Combine(AppLog.Root,"OneTaskBar.exe"),"--guard-fixture"){UseShellExecute=false,CreateNoWindow=true});
                    guard=GuardProcess.Start(child,Path.Combine(AppLog.Root,"OneTaskBar.exe"),data);
                    string ready=Path.Combine(data,"guard-"+child.Id+".ready");for(int i=0;i<60&&!File.Exists(ready);i++)await Task.Delay(50);if(!File.Exists(ready))throw new Exception("Watchdog hook did not become ready");await Task.Delay(450);
                    if(!File.ReadAllText(ready).EndsWith("|packaged=False"))throw new Exception("Watchdog inherited a package identity");
                    double cpuBefore=guard.TotalProcessorTime.TotalMilliseconds;var idleWatch=Stopwatch.StartNew();await Task.Delay(65000);double idleSeconds=idleWatch.Elapsed.TotalSeconds,idleOneCore=(guard.TotalProcessorTime.TotalMilliseconds-cpuBefore)/idleWatch.Elapsed.TotalMilliseconds*100;if(guard.HasExited||idleOneCore>5)throw new Exception("Watchdog exited or busy-spins while idle: "+idleOneCore);
                    using(var duplicate=GuardProcess.Start(child,Path.Combine(AppLog.Root,"OneTaskBar.exe"),data)){if(!duplicate.WaitForExit(2000)||duplicate.ExitCode!=4)throw new Exception("Duplicate watchdog not rejected");}
                    IntPtr own=new WindowInteropHelper(window).Handle;Native.Activate(own);await Task.Delay(50);if(Native.GetForegroundWindow()!=own)throw new Exception("Isolated test window is not focused; input was not sent");
                    bool disabledSent=SystemPanels.ProbeEmergency();await Task.Delay(600);bool disabledSurvived=!child.HasExited;if(!disabledSurvived)throw new Exception("Disabled emergency option still terminated the fixture");
                    File.WriteAllText(settings,"{\"EmergencyExit\":true}");await Task.Delay(500);Native.Activate(own);if(Native.GetForegroundWindow()!=own)throw new Exception("Test focus lost");var watch=Stopwatch.StartNew();bool enabledSent=SystemPanels.ProbeEmergency();for(int i=0;i<80&&!child.HasExited;i++)await Task.Delay(25);bool terminated=child.HasExited;for(int i=0;i<40&&!guard.HasExited;i++)await Task.Delay(25);
                    code=disabledSent&&disabledSurvived&&enabledSent&&terminated&&guard.HasExited?0:1;AppLog.Json("recovery-probe.json",new {passed=code==0,idleSeconds=idleSeconds,idleOneCorePercent=idleOneCore,unpackagedWatchdog=true,duplicateRejected=true,disabledSurvived=disabledSurvived,hungFixtureTerminated=terminated,watchdogExited=guard.HasExited,milliseconds=watch.ElapsedMilliseconds});
                }catch(Exception error){AppLog.Json("recovery-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(child!=null){try{if(!child.HasExited)child.Kill();}catch{}child.Dispose();}if(guard!=null){try{if(!guard.HasExited)guard.WaitForExit(2000);}catch{}guard.Dispose();}window.Close();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
        internal static int Typing()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;ShellController shell=null;bool native=Preferences.Current.NativeSearch;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Preferences.Current.NativeSearch=false;shell=new ShellController(true);shell.ToggleDrawer(null);await Task.Delay(230);shell.Drawer.Search.Focus();IntPtr own=new WindowInteropHelper(shell.Drawer).Handle;Native.Activate(own);if(Native.GetForegroundWindow()!=own)throw new Exception("Input test window is not focused");
                    bool sent=SystemPanels.ProbeText("chat");await Task.Delay(400);bool direct=sent&&shell.Finder!=null&&shell.Finder.IsVisible&&shell.Finder.Query.Text=="chat";
                    shell.ToggleDrawer(null);await Task.Delay(250);var input=shell.Drawer.Search;
                    input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,input,"ㅊ")){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});input.Text="ㅊ";await Task.Delay(80);bool deferred=shell.Finder!=null&&!shell.Finder.IsVisible;
                    input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,input,"챗")){RoutedEvent=TextCompositionManager.PreviewTextInputUpdateEvent});input.Text="챗";await Task.Delay(80);deferred&=!shell.Finder.IsVisible;
                    input.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,new TextComposition(InputManager.Current,input,"챗")){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});await Task.Delay(300);bool committed=shell.Finder.IsVisible&&shell.Finder.Query.Text=="챗";
                    shell.Finder.Query.Text="소리";await Task.Delay(250);bool rounded=shell.Finder.Radius==32&&shell.Finder.SoftCorners;
                    code=direct&&deferred&&committed&&rounded?0:1;AppLog.Json("typing-probe.json",new {passed=code==0,directTyping=direct,compositionDeferred=deferred,hangulCommitted=committed,searchRounded=rounded,query=shell.Finder.Query.Text});
                }catch(Exception error){AppLog.Json("typing-probe.json",new {passed=false,error=error.ToString()});}
                finally{Preferences.Current.NativeSearch=native;if(shell!=null)shell.Dispose();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
