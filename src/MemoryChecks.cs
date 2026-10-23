using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class MemoryChecks
    {
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;
            application.Dispatcher.BeginInvoke(new Action(async()=> {
                try{
                    Program.Inspectable=true;shell=new ShellController(true);shell.Apps=Enumerable.Range(1,6).Select(i=>new AppEntry {Name="앱 "+i,Target="fixture:"+i}).ToList();shell.StartPins.Replace(shell.Apps);shell.Quick=new QuickWindow(shell);shell.Center=new NotificationWindow(shell);shell.Detail=new DetailWindow(shell,"focus");shell.Drawer=new DrawerWindow(shell);shell.Finder=new SearchWindow(shell);shell.StartMenu=new StartWindow(shell);shell.Welcome=new WelcomeWindow(shell);
                    shell.Drawer.Configure(720,650);shell.Drawer.Reload();shell.StartMenu.FixturePhone(StartDataChecks.Phone());shell.StartMenu.FixtureRecent(new System.Collections.Generic.List<StartRecent>());shell.StartMenu.Reload();int baseline=LiveBackdrop.InstanceCount;bool timer=false;
                    foreach(var panel in new GlassWindow[]{shell.Quick,shell.Center,shell.Detail,shell.Drawer,shell.Finder,shell.StartMenu,shell.Welcome}){panel.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,30,30,Math.Min(720,panel.Width),Math.Min(650,panel.Height));await Task.Delay(30);if(panel==shell.Detail)timer=shell.Detail.TimerActive;panel.HideImmediately();}
                    await Task.Delay(120);bool stopped=timer&&!shell.Detail.TimerActive;int allocated=LiveBackdrop.InstanceCount;shell.ReleaseDormantPanels(DateTime.UtcNow);bool warm=shell.Quick!=null&&shell.StartMenu!=null&&shell.RetiredPanels==0;shell.ReleaseDormantPanels(DateTime.UtcNow.AddSeconds(25));bool released=shell.Quick==null&&shell.Center==null&&shell.Detail==null&&shell.Drawer==null&&shell.Finder==null&&shell.StartMenu==null&&shell.Welcome==null&&shell.RetiredPanels==7;bool backdrops=LiveBackdrop.InstanceCount<=baseline&&LiveBackdrop.OrphanCount==0;
                    shell.ToggleDrawer(null);await Task.Delay(100);bool reopen=shell.Drawer!=null&&shell.Drawer.IsVisible&&shell.Drawer.AppKeys.Length==6;bool passed=stopped&&warm&&released&&backdrops&&reopen;AppLog.Json("memory-lifecycle-probe.json",new {passed=passed,detailTimerStopsWhenHidden=stopped,recentPanelsRemainWarm=warm,idlePanelsReleased=released,compositionResourcesReleased=backdrops,reopenPreservesApps=reopen,allocatedBackdrops=allocated,releasedPanels=shell.RetiredPanels,retirementDelaySeconds=20,iconCacheLimit=AppIcons.CacheLimit,forcedGc=false,workingSetTrim=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("memory-lifecycle-probe.json",new {passed=false,error=error.ToString()});}finally{if(shell!=null)shell.Dispose();application.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
