using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WorkspaceChecks
    {
        static uint State(){var data=new Native.AppBarData {Size=(uint)Marshal.SizeOf(typeof(Native.AppBarData))};return (uint)Native.SHAppBarMessage(4,ref data).ToUInt64();}
        internal static int Run()
        {
            bool held=false;using(var mutex=new Mutex(false,@"Local\OneTaskBar.Desktop")){
                try{try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held){AppLog.Json("workspace-probe.json",new {passed=false,error="Close the installed app before the isolated work-area test."});return 1;}
                    var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;var original=State();int edge=Preferences.Current.DockEdge;DesktopLease lease=null;var windows=new List<Window>();var samples=new List<object>();
                    app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                        var before=new Dictionary<string,Native.RectI>();foreach(var screen in System.Windows.Forms.Screen.AllScreens)before[screen.DeviceName]=Native.WorkArea(screen);
                        lease=new DesktopLease {Active=true,TaskbarState=original,RestoreTaskbarState=true,Session=Guid.NewGuid().ToString("N")};lease.Save();TaskbarMask.Active=true;Native.HideTaskbars();TaskbarSpace.Acquire(lease);await Task.Delay(250);
                        if((State()&1)==0||Native.TaskbarAutoHide()!=((original&1)!=0))throw new InvalidOperationException("Physical and logical taskbar states were not separated");
                        foreach(var screen in System.Windows.Forms.Screen.AllScreens){var host=new Window {Width=1,Height=1,WindowStyle=WindowStyle.None,ShowInTaskbar=false,ShowActivated=false};windows.Add(host);double scale=Native.PrepareMonitor(host,screen.Bounds);var b=screen.Bounds;
                            for(int side=0;side<4;side++){Preferences.Current.DockEdge=side;using(var reserve=new AppBarReservation(screen,scale,false)){await Task.Delay(200);var actual=Native.WorkArea(screen);int span=(int)Math.Round(68*scale);var expected=new Native.RectI {Left=b.Left+(side==0?span:0),Top=b.Top+(side==1?span:0),Right=b.Right-(side==2?span:0),Bottom=b.Bottom-(side==3?span:0)};
                                if(Math.Abs(actual.Left-expected.Left)>3||Math.Abs(actual.Top-expected.Top)>3||Math.Abs(actual.Right-expected.Right)>3||Math.Abs(actual.Bottom-expected.Bottom)>3)throw new InvalidOperationException("Unexpected work-area reservation at edge "+side+": "+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(actual));samples.Add(new {monitor=screen.DeviceName,edge=side,scale=scale,work=actual});reserve.SetAutoHide(true);await Task.Delay(100);actual=Native.WorkArea(screen);if(Math.Abs(actual.Bottom-b.Bottom)>3||Math.Abs(actual.Left-b.Left)>3||Math.Abs(actual.Top-b.Top)>3||Math.Abs(actual.Right-b.Right)>3)throw new InvalidOperationException("Auto-hide retained work-area space");}
                            }
                        }
                        Native.SetTaskbarAutoHide(true);if(!Native.TaskbarAutoHide()||(State()&1)==0)throw new InvalidOperationException("Logical auto-hide enable");Native.SetTaskbarAutoHide(false);if(Native.TaskbarAutoHide()||(State()&1)==0)throw new InvalidOperationException("Logical auto-hide disable kept physical work area leased");Native.SetTaskbarAutoHide((original&1)!=0);
                        TaskbarMask.Active=false;DesktopLease.Recover(lease.Session);lease=null;await Task.Delay(300);if(State()!=original)throw new InvalidOperationException("Native taskbar flags were not restored");foreach(var screen in System.Windows.Forms.Screen.AllScreens){var actual=Native.WorkArea(screen);var prior=before[screen.DeviceName];if(Math.Abs(actual.Left-prior.Left)>3||Math.Abs(actual.Top-prior.Top)>3||Math.Abs(actual.Right-prior.Right)>3||Math.Abs(actual.Bottom-prior.Bottom)>3)throw new InvalidOperationException("Original work area not restored");}
                        AppLog.Json("workspace-probe.json",new {passed=true,samples=samples,allFourEdges=true,autoHideReturnsSpace=true,originalTaskbarStateRestored=true,originalWorkAreaRestored=true});result=0;
                    }catch(Exception error){AppLog.Json("workspace-probe.json",new {passed=false,error=error.ToString(),samples=samples});}finally{foreach(var window in windows)window.Close();Preferences.Current.DockEdge=edge;TaskbarMask.Active=false;if(lease!=null){lease.TaskbarState=original;lease.Save();DesktopLease.Recover(lease.Session);}app.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return app.Run();
                }finally{if(held)mutex.ReleaseMutex();}
            }
        }
    }
}
