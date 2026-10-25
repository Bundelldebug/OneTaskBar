using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class StartSizeChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;shell=new ShellController(true);shell.Apps=Enumerable.Range(1,24).Select(i=>new AppEntry {Name="앱 "+i,Target="fixture:"+i}).ToList();shell.StartPins.Replace(shell.Apps.Take(11));var menu=new StartWindow(shell);shell.StartMenu=menu;menu.FixturePhone(StartDataChecks.Phone());menu.RefreshPins();menu.FixtureRecent(Enumerable.Range(1,20).Select(i=>new StartRecent {App=new AppEntry {Name="최근 항목 "+i,Target="fixture:recent:"+i},Time=DateTime.Now.AddMinutes(-i)}).ToList());
                var screen=System.Windows.Forms.Screen.PrimaryScreen;var sizes=new List<object>();
                foreach(int count in new[]{11,18,24}){
                    shell.StartPins.Replace(shell.Apps.Take(count));menu.Configure(940,620);menu.RefreshPins();double preferred=menu.PreferredHomeHeight();menu.Configure(940,preferred);menu.ShowAt(screen.Bounds,20,20,940,preferred);await Task.Delay(120);menu.UpdateLayout();var last=(FrameworkElement)menu.RecentItems.Children[7];double bottom=last.TranslatePoint(new Point(0,last.ActualHeight),menu.ContentScroll).Y;
                    Require(menu.RecentCount==8&&bottom<=menu.ContentScroll.ActualHeight+.5,"Four recent rows are clipped with "+count+" pins: "+bottom+" / "+menu.ContentScroll.ActualHeight+"; preferred="+preferred);Require(menu.ContentScroll.ScrollableHeight<1,"Unnecessary home page scrolling at its preferred size");sizes.Add(new {pins=count,height=preferred,recentBottom=bottom,viewport=menu.ContentScroll.ActualHeight});
                    if(count==11)Program.Save(menu.Surface,940,(int)preferred,Path.Combine(AppLog.Data,"start-four-rows.png"));
                    double before=menu.Height;menu.ShowRecentPage(true);await Task.Delay(60);Require(menu.RecentPage&&menu.RecentCount==20&&menu.Height==before,"More must remain a separate page at the fitted height");menu.ShowRecentPage(false);await Task.Delay(60);Require(!menu.RecentPage&&menu.RecentCount==8&&menu.Height==before,"Back restores the four-row home page");
                }
                var fit=StartMenuLayout.Fit(new System.Drawing.Rectangle(-1280,0,1280,720),new Native.RectI {Left=-1280,Right=0,Bottom=652},1,menu.PreferredHomeHeight());menu.Configure(fit.Width,fit.Height);menu.ShowAt(screen.Bounds,20,20,fit.Width,fit.Height);await Task.Delay(100);Require(menu.ContentScroll.ScrollableHeight>0&&fit.Y+fit.Height<=644.01,"Small displays must scroll within their work area");Program.Save(menu.Surface,(int)fit.Width,(int)fit.Height,Path.Combine(AppLog.Data,"start-small-display.png"));
                AppLog.Json("start-size-probe.json",new {passed=true,fourRows=sizes,smallDisplayScroll=true,monitorDpiAndNegativeOrigins=true,morePagePreserved=true,osInputSent=false});result=0;
            }catch(Exception error){AppLog.Json("start-size-probe.json",new {passed=false,error=error.ToString()});}finally{if(shell!=null)shell.Dispose();application.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
