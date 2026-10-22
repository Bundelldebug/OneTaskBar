using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class StartKeyChecks
    {
        sealed class Observer : IDisposable
        {
            delegate IntPtr Keyboard(int code,IntPtr message,IntPtr data);
            [DllImport("user32.dll")]static extern IntPtr SetWindowsHookEx(int kind,Keyboard callback,IntPtr module,uint thread);
            [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
            [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
            [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
            readonly Keyboard callback;readonly IntPtr hook;internal readonly List<string> Events=new List<string>();
            internal Observer(){callback=Read;hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);}
            IntPtr Read(int code,IntPtr message,IntPtr data){if(code>=0){int key=Marshal.ReadInt32(data);if(key==0x5b||key==0x5c||key==0x87||key==0x10||key==0xa0||key==0xa1){bool up=message.ToInt32()==0x101||message.ToInt32()==0x105;string name=key==0x5b||key==0x5c?"Win":key==0x87?"F24":"Shift";Events.Add(name+(up?"Up":"Down"));}}return CallNextHookEx(hook,code,message,data);}
            public void Dispose(){if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);}
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;Observer observer=null;VolumeKeys keys=null;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    Program.Inspectable=true;shell=new ShellController(true);observer=new Observer();bool custom=true;int opened=0;
                    keys=new VolumeKeys {Enabled=true,InterceptStart=()=>custom,OnStart=()=> {opened++;app.Dispatcher.BeginInvoke(new Action(()=>shell.ToggleStart(null)));}};
                    bool left=await Task.Run(()=>SystemPanels.ProbeStartTap());await Task.Delay(430);bool first=left&&opened==1&&shell.StartMenu!=null&&shell.StartMenu.IsVisible&&observer.Events.Count==0;
                    bool right=await Task.Run(()=>SystemPanels.ProbeStartTap(0x5c));await Task.Delay(300);bool second=right&&opened==2&&!shell.StartMenu.IsVisible&&observer.Events.Count==0;
                    observer.Events.Clear();bool chord=await Task.Run(()=>SystemPanels.ProbeStartChord());await Task.Delay(160);bool ordered=chord&&opened==2&&observer.Events.SequenceEqual(new[]{"WinDown","F24Down","F24Up","WinUp"});var order=observer.Events.ToArray();
                    observer.Events.Clear();bool shifted=await Task.Run(()=>SystemPanels.ProbeStartChord(true));await Task.Delay(160);bool modifiers=shifted&&opened==2&&observer.Events.SequenceEqual(new[]{"WinDown","ShiftDown","F24Down","F24Up","ShiftUp","WinUp"});
                    custom=false;observer.Events.Clear();bool native=await Task.Run(()=>SystemPanels.ProbeStartChord());await Task.Delay(150);bool passthrough=native&&opened==2&&observer.Events.SequenceEqual(new[]{"WinDown","F24Down","F24Up","WinUp"});
                    bool noNative=!LauncherChecks.StartVisible()&&ReferenceChecks.FocusName()!="SearchHost";bool passed=first&&second&&ordered&&modifiers&&passthrough&&noNative;AppLog.Json("start-key-probe.json",new {passed=passed,leftWinNeverReachesShell=first,rightWinNeverReachesShell=second,chordOrderPreserved=ordered,chordOrder=order,shiftChordPreserved=modifiers,nativeModePassesThrough=passthrough,noNativeStartOrSearch=noNative,customInvocations=opened});result=passed?0:1;
                }catch(Exception error){AppLog.Json("start-key-probe.json",new {passed=false,error=error.ToString()});}
                finally {if(keys!=null)keys.Dispose();if(observer!=null)observer.Dispose();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
