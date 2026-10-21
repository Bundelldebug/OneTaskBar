using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class OsdChecks
    {
        internal static int Recovery()
        {
            bool left=false,restored=false;int exit=-1;try{
                string executable=Process.GetCurrentProcess().MainModule.FileName;using(var child=Process.Start(new ProcessStartInfo(executable,"--osd-mask-fixture"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden})){if(!child.WaitForExit(5000))throw new Exception("OSD recovery fixture did not exit");exit=child.ExitCode;}
                var record=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(System.IO.File.ReadAllText(System.IO.Path.Combine(AppLog.Data,"osd-mask-fixture.json")));var windows=((System.Collections.IEnumerable)record["windows"]).Cast<object>().Select(v=>new IntPtr(Convert.ToInt64(v))).ToArray();left=windows.Any(NativeOsdGate.IsMasked);NativeOsdGate.Recover();restored=windows.All(h=>!NativeOsdGate.IsMasked(h));
            }finally{NativeOsdGate.Recover();}bool passed=exit==0&&left&&restored;AppLog.Json("osd-recovery-probe.json",new {passed=passed,fixtureExitedWithoutDispose=exit==0,maskSurvivedFixture=left,nativeOpacityRestored=restored});return passed?0:1;
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;double level=-1;bool muted=false;int brightness=-1,result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    shell=new ShellController();shell.Audio.Read();if(!shell.Audio.Available)throw new Exception("No audio endpoint");level=shell.Audio.Value;muted=shell.Audio.Muted;brightness=await Task.Run(()=>BrightnessService.Read());shell.Brightness=brightness;shell.Desktop=true;shell.PrepareNativeOsd();await Task.Delay(250);int prepared=shell.OsdPrepared;
                    var watch=Stopwatch.StartNew();bool sent=Native.PostMessage(Native.FindWindow("Shell_TrayWnd",null),0x319,IntPtr.Zero,new IntPtr((level>=98?9:10)<<16));
                    for(int i=0;i<60&&(!shell.HudVisible||shell.HudKind!="volume");i++)await Task.Delay(20);long volumeMs=watch.ElapsedMilliseconds;bool volume=shell.HudVisible&&shell.HudKind=="volume";await Task.Delay(100);int hidden=shell.OsdSuppressed;
                    bool bright=false;long brightMs=-1;if(brightness>=0){watch.Restart();await Task.Run(()=>BrightnessService.Set(brightness>0?brightness-1:1));for(int i=0;i<60&&shell.HudKind!="brightness";i++)await Task.Delay(20);brightMs=watch.ElapsedMilliseconds;bright=shell.HudVisible&&shell.HudKind=="brightness";}
                    result=sent&&volume&&hidden>0&&(brightness<0||bright&&shell.OsdPrimedShows>0)?0:1;AppLog.Json("osd-probe.json",new {passed=result==0,nativeVolumeCommand=sent,maskedBeforeKey=prepared,nativeShowAlreadyMasked=shell.OsdPrimedShows,customVolume=volume,nativeWindowsSuppressed=hidden,volumeMilliseconds=volumeMs,brightnessSupported=brightness>=0,brightnessEvent=bright,brightnessMilliseconds=brightMs});
                }catch(Exception e){AppLog.Json("osd-probe.json",new {passed=false,error=e.ToString()});}
                finally {if(shell!=null){shell.Desktop=false;if(level>=0){try{shell.Audio.Set(level);shell.Audio.Mute(muted);}catch{}}if(brightness>=0){try{BrightnessService.Set(brightness);}catch{}}var masks=shell.OsdMasks;shell.Dispose();bool restored=System.Linq.Enumerable.All(masks,h=>!NativeOsdGate.IsMasked(h));var report=new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<System.Collections.Generic.Dictionary<string,object>>(System.IO.File.ReadAllText(System.IO.Path.Combine(AppLog.Data,"osd-probe.json")));report["nativeMaskRestored"]=restored;if(!restored){result=1;report["passed"]=false;}AppLog.Json("osd-probe.json",report);}app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
