using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class OsdChecks
    {
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;double level=-1;bool muted=false;int brightness=-1,result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=> {
                try {
                    shell=new ShellController();shell.Audio.Read();if(!shell.Audio.Available)throw new Exception("No audio endpoint");level=shell.Audio.Value;muted=shell.Audio.Muted;brightness=await Task.Run(()=>BrightnessService.Read());shell.Brightness=brightness;shell.Desktop=true;await Task.Delay(250);
                    var watch=Stopwatch.StartNew();bool sent=Native.PostMessage(Native.FindWindow("Shell_TrayWnd",null),0x319,IntPtr.Zero,new IntPtr((level>=98?9:10)<<16));
                    for(int i=0;i<60&&(!shell.HudVisible||shell.HudKind!="volume");i++)await Task.Delay(20);long volumeMs=watch.ElapsedMilliseconds;bool volume=shell.HudVisible&&shell.HudKind=="volume";await Task.Delay(100);int hidden=shell.OsdSuppressed;
                    bool bright=false;long brightMs=-1;if(brightness>=0){watch.Restart();await Task.Run(()=>BrightnessService.Set(brightness>0?brightness-1:1));for(int i=0;i<60&&shell.HudKind!="brightness";i++)await Task.Delay(20);brightMs=watch.ElapsedMilliseconds;bright=shell.HudVisible&&shell.HudKind=="brightness";}
                    result=sent&&volume&&hidden>0&&(brightness<0||bright)?0:1;AppLog.Json("osd-probe.json",new {passed=result==0,nativeVolumeCommand=sent,customVolume=volume,nativeWindowsSuppressed=hidden,volumeMilliseconds=volumeMs,brightnessSupported=brightness>=0,brightnessEvent=bright,brightnessMilliseconds=brightMs});
                }catch(Exception e){AppLog.Json("osd-probe.json",new {passed=false,error=e.ToString()});}
                finally {if(shell!=null){shell.Desktop=false;if(level>=0){try{shell.Audio.Set(level);shell.Audio.Mute(muted);}catch{}}if(brightness>=0){try{BrightnessService.Set(brightness);}catch{}}shell.Dispose();}app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
