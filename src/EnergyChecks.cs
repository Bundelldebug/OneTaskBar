using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class EnergyChecks
    {
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int code=1;Window window=null;AudioService audio=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    var title=new MarqueeTitle {Text="Battery marquee verification",ConservePower=true,Running=true};title.Width=Math.Max(12,title.NaturalWidth-12);
                    window=new Window {Title="One TaskBar · energy verification",Width=title.Width+36,Height=84,Content=title,ShowActivated=false,ShowInTaskbar=false,WindowStyle=WindowStyle.ToolWindow};window.Show();
                    await Task.Delay(4200);bool stopped=!title.IsAnimating&&title.Offset==0;int renders=title.RenderCount;await Task.Delay(500);bool staticFrame=title.RenderCount==renders;
                    title.Engaged=true;await Task.Delay(1450);bool hover=title.IsAnimating&&title.Offset>0;title.Engaged=false;title.ConservePower=false;await Task.Delay(300);bool ac=title.IsAnimating;
                    window.Hide();await Task.Delay(100);bool hidden=!title.IsAnimating;
                    audio=new AudioService();audio.Read();bool notifications=audio.Available&&audio.NotificationsRegistered,eventReceived=false;double original=0;bool restored=false;
                    if(notifications){original=audio.ExactValue;var received=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);audio.Changed=()=>received.TrySetResult(true);
                        try{audio.Set(original>=99?original-1:original+1);eventReceived=await Task.WhenAny(received.Task,Task.Delay(2000))==received.Task;}
                        finally{audio.Set(original);audio.Read();restored=Math.Abs(audio.ExactValue-original)<.001;}}
                    code=stopped&&staticFrame&&hover&&ac&&hidden&&notifications&&eventReceived&&restored?0:1;
                    AppLog.Json("energy-probe.json",new {passed=code==0,batteryMarqueeStopsAfterOnePass=stopped,noIdleTitleRedraw=staticFrame,hoverResumesMarquee=hover,acMarqueeContinues=ac,hiddenTitleStops=hidden,audioCallbackRegistered=notifications,audioEventReceived=eventReceived,originalVolumeRestored=restored});
                }catch(Exception error){AppLog.Json("energy-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(audio!=null)audio.Dispose();if(window!=null)window.Close();app.Shutdown(code);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
