using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WindowActivationChecks
    {
        const int Command=0x804E;
        [DllImport("kernel32.dll")]static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")]static extern bool AttachThreadInput(uint first,uint second,bool attach);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
        [DllImport("user32.dll")]static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll")]static extern IntPtr GetActiveWindow();
        static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        static async Task Wait(Func<bool> ready,int timeout=2000){var watch=Stopwatch.StartNew();while(!ready()){if(watch.ElapsedMilliseconds>=timeout)throw new TimeoutException("Window activation fixture deadline");await Task.Delay(15);}}
        static bool Above(IntPtr first,IntPtr second){var found=new List<IntPtr>();Native.EnumWindows((h,l)=>{if(h==first||h==second)found.Add(h);return true;},IntPtr.Zero);return found.Count==2&&found[0]==first;}
        static void Detached(){uint pid;var active=GetActiveWindow();if(active!=IntPtr.Zero){Native.GetWindowThreadProcessId(active,out pid);Require(pid==(uint)Process.GetCurrentProcess().Id,"A foreign active window leaked into the calling input queue");}Require(WindowActivation.AttachedQueues==0,"Every temporary input attachment must be detached");}
        internal static int Source(string name)
        {
            if(name!="a"&&name!="b")return 2;var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var window=new Window {Title="One Taskbar 전환 검사 "+name.ToUpperInvariant(),Width=470,Height=300,Left=name=="a"?40:550,Top=320,Background=name=="a"?Brushes.SteelBlue:Brushes.SeaGreen,Content=new TextBox {Text="다른 프로세스의 실제 창",FontSize=26,Margin=new Thickness(24),VerticalContentAlignment=VerticalAlignment.Center}};
            window.SourceInitialized+=(s,e)=>{var handle=new WindowInteropHelper(window).Handle;HwndSource.FromHwnd(handle).AddHook((IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled)=>{if(message!=Command)return IntPtr.Zero;handled=true;int state=wp.ToInt32();if(state==100)Thread.Sleep(500);else if(state==0||state==2||state==1)window.WindowState=state==2?WindowState.Maximized:state==1?WindowState.Minimized:WindowState.Normal;return IntPtr.Zero;});};
            window.Loaded+=(s,e)=>{var handle=new WindowInteropHelper(window).Handle;File.WriteAllText(Path.Combine(AppLog.Data,"focus-source-"+name+".txt"),handle.ToInt64().ToString());};window.Closed+=(s,e)=>app.Shutdown();window.Show();return app.Run();
        }
        // Arrange only these two owned fixture processes, without a synthetic OS key/click.
        static void Prepare(IntPtr target)
        {
            uint pid;uint current=GetCurrentThreadId(),foreground=Native.GetWindowThreadProcessId(Native.GetForegroundWindow(),out pid),destination=Native.GetWindowThreadProcessId(target,out pid);bool first=false,second=false;
            try{if(foreground!=0&&foreground!=current)first=AttachThreadInput(current,foreground,true);if(destination!=0&&destination!=current&&destination!=foreground)second=AttachThreadInput(current,destination,true);Native.SetForegroundWindow(target);}finally{if(second)AttachThreadInput(current,destination,false);if(first)AttachThreadInput(current,foreground,false);}
        }
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var children=new List<Process>();var handles=new List<IntPtr>();WindowSwitcherManager manager=null;ShellController shell=null;IDisposable pointer=null;bool oldNative=Preferences.Current.NativeAltTab;int result=1;var cases=new List<object>();
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;Preferences.Current.NativeAltTab=false;pointer=MenuDismiss.WithSyntheticPointerForFixture();
                foreach(string name in new[]{"a","b"}){string file=Path.Combine(AppLog.Data,"focus-source-"+name+".txt");var start=new ProcessStartInfo(Path.Combine(AppLog.Root,"OneTaskBar.exe"),"--window-focus-source "+name){UseShellExecute=false,WorkingDirectory=AppLog.Root};var child=Process.Start(start);children.Add(child);await Wait(()=>File.Exists(file)&&new FileInfo(file).Length>0);var handle=new IntPtr(Int64.Parse(File.ReadAllText(file)));uint pid;Native.GetWindowThreadProcessId(handle,out pid);Require(pid==(uint)child.Id,"Fixture HWND must belong to its own child process");handles.Add(handle);}
                var a=handles[0];var b=handles[1];var entries=handles.Select((h,i)=>new SwitchItem {Window=h,Title="별도 프로세스 "+i,App=new AppEntry {Name="실제 앱 전환 "+i,Target="fixture:external:"+i,Icon=ArtworkPalette.Sample(i==0?Colors.SteelBlue:Colors.SeaGreen)}}).ToList();shell=new ShellController(true);manager=new WindowSwitcherManager(shell,()=>entries.ToList());manager.Update(new AppEntry[0]);
                foreach(int state in new[]{0,2,1}){
                    PostMessage(b,Command,new IntPtr(state),IntPtr.Zero);await Wait(()=>state==1?Native.IsIconic(b):!Native.IsIconic(b)&&IsZoomed(b)==(state==2));Native.RectI before;Native.GetWindowRect(b,out before);Prepare(a);await Wait(()=>Native.GetForegroundWindow()==a);Require(manager.Cycle(false),"Start actual cross-process switch");if(state!=1)await Task.Delay(140);manager.Finish(true);await Task.Delay(180);
                    bool foreground=Native.GetForegroundWindow()==b;Native.RectI after;Native.GetWindowRect(b,out after);bool stateKept=state!=2||IsZoomed(b)&&before.Equals(after);cases.Add(new {state=state,targetForeground=foreground,minimized=Native.IsIconic(b),maximized=IsZoomed(b),boundsPreserved=stateKept});Require(foreground&&!Native.IsIconic(b)&&stateKept&&Above(b,a),"Selected external window did not become foreground, or its maximized state changed: "+state);Detached();
                }
                int topmost=Native.GetWindowLong(b,-20)&8;Require(Native.GetForegroundWindow()==b,"Already active target remains foreground");Require(WindowActivation.TryActivate(b)&&WindowActivation.TryActivate(b),"Re-select the already active target");await Wait(()=>Above(b,a));Require((Native.GetWindowLong(b,-20)&8)==topmost,"Window activation must not change always-on-top policy");Detached();
                var stalls=new List<long>();foreach(var busy in new[]{b,a}){Prepare(a);await Wait(()=>Native.GetForegroundWindow()==a);PostMessage(busy,Command,new IntPtr(100),IntPtr.Zero);await Task.Delay(35);var watch=Stopwatch.StartNew();WindowActivation.TryActivate(b);stalls.Add(watch.ElapsedMilliseconds);Require(watch.ElapsedMilliseconds<300,"Unresponsive applications must not stall the switcher");Detached();await Task.Delay(550);}
                Prepare(a);await Wait(()=>Native.GetForegroundWindow()==a);manager.Cycle(false);await Task.Delay(100);manager.Finish(false);await Task.Delay(80);Require(Native.GetForegroundWindow()==a,"Cancellation keeps the previous external foreground");
                Require(!manager.TimerActive&&manager.View==null&&!application.Windows.OfType<SwitchThumbnail>().Any(),"Cross-process switch cleans up the chooser");AppLog.Json("window-activation-probe.json",new {passed=true,cases=cases,separateProcessTargets=true,actualForegroundAndZOrderVerified=true,normalAndMaximizedRaised=true,minimizedRestored=true,alreadyActiveWindowRaised=true,topmostPolicyUnchanged=true,inputQueuesDetached=true,unresponsiveProbeMilliseconds=stalls,cancelPreservesForeground=true,osInputSent=false,onlyFixtureWindowsUsed=true});result=0;
            }catch(Exception error){AppLog.Json("window-activation-probe.json",new {passed=false,error=error.ToString(),cases=cases});}finally{
                if(manager!=null)manager.Dispose();if(shell!=null)shell.Dispose();for(int i=0;i<children.Count;i++){var child=children[i];if(!child.HasExited){if(i<handles.Count)PostMessage(handles[i],0x10,IntPtr.Zero,IntPtr.Zero);if(!child.WaitForExit(1500))child.Kill();}child.Dispose();}if(pointer!=null)pointer.Dispose();Preferences.Current.NativeAltTab=oldNative;application.Shutdown(result);
            }}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
