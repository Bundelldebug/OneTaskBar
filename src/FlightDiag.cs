using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // A manual diagnostic, not a fixture: it flies a real window (Character Map) into and out of a real Dynamic Island on
    // this display, the same way a media app's window does, and records how evenly the frames arrived.
    // Run with --flight-diag (stop the installed app first). Results: flight-diag.json and media-flight-timing.json.
    internal static class FlightDiag
    {
        internal static int Run()
        {
            FrameRate.Raise();
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;
            Process process=null;
            DynamicIsland island=null;
            application.Dispatcher.BeginInvoke(new Action(async()=>
            {
                var rounds=new List<object>();
                try
                {
                    Program.Inspectable=true;
                    var screen=Forms.Screen.PrimaryScreen;
                    double dpi;
                    using(var graphics=System.Drawing.Graphics.FromHwnd(IntPtr.Zero))dpi=graphics.DpiX/96.0;
                    island=new DynamicIsland(command=>{},seek=>Task.FromResult(true),()=>{},id=>{});
                    var media=new MediaState {Available=true,Active=true,Playing=true,Title="비행 진단",Artist="테스트"};
                    island.Present(media,"",screen,dpi,screen.WorkingArea,false);
                    await Task.Delay(900);
                    island.Present(media,"",screen,dpi,screen.WorkingArea,false);
                    process=Process.Start("charmap.exe");
                    IntPtr window=IntPtr.Zero;
                    for(int i=0;i<60&&window==IntPtr.Zero;i++)
                    {
                        await Task.Delay(100);
                        process.Refresh();
                        window=process.MainWindowHandle;
                    }
                    if(window==IntPtr.Zero)throw new Exception("The window did not open");
                    Native.SetWindowPos(window,IntPtr.Zero,(int)(screen.Bounds.Width*.2),(int)(screen.Bounds.Height*.25),0,0,0x15);
                    await Task.Delay(600);
                    for(int round=0;round<4;round++)
                    {
                        Rect capsule;IntPtr owner;
                        if(!island.MediaCapsule(screen,out capsule,out owner))throw new Exception("The island has no capsule");
                        Native.Activate(window);await Task.Delay(400);
                        Native.RectI rect;Native.GetWindowRect(window,out rect);
                        var bounds=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);
                        var image=MediaWindowSnapshot.Capture(window,bounds,()=>true);
                        int enterFrames,restoreFrames;double enterGap,restoreGap,enterFirst,restoreFirst;
                        using(var enter=new MediaWindowMotion(null,null))
                        {
                            enter.Begin(window,null,owner,bounds,capsule,true,false,image,null);
                            Native.ShowWindow(window,6);
                            await Task.Delay(800);
                            enterFrames=enter.FrameCount;enterGap=enter.MaximumFrameGap;enterFirst=enter.FirstProgress;
                        }
                        await Task.Delay(300);
                        using(var restore=new MediaWindowMotion(null,null))
                        {
                            restore.Begin(window,null,owner,capsule,bounds,true,true,image,null);
                            Native.ShowWindow(window,9);
                            await Task.Delay(800);
                            restoreFrames=restore.FrameCount;restoreGap=restore.MaximumFrameGap;restoreFirst=restore.FirstProgress;
                        }
                        rounds.Add(new {round=round,enterFrames=enterFrames,enterMaxGapMs=Math.Round(enterGap,1),enterFirstProgress=Math.Round(enterFirst,3),restoreFrames=restoreFrames,restoreMaxGapMs=Math.Round(restoreGap,1),restoreFirstProgress=Math.Round(restoreFirst,3)});
                        await Task.Delay(400);
                    }
                    AppLog.Json("flight-diag.json",new {passed=true,dpi=dpi,displayHz=FrameRate.Refresh(),targetHz=FrameRate.Target,renderTier=System.Windows.Media.RenderCapability.Tier>>16,rounds=rounds.ToArray()});
                    result=0;
                }
                catch(Exception error)
                {
                    AppLog.Json("flight-diag.json",new {passed=false,error=error.ToString(),rounds=rounds.ToArray()});
                }
                finally
                {
                    try{if(process!=null&&!process.HasExited)process.Kill();}catch(Exception){}
                    try{if(island!=null)island.Close();}catch(Exception){}
                    application.Shutdown(result);
                }
            }),DispatcherPriority.ApplicationIdle);
            return application.Run();
        }
    }
}
