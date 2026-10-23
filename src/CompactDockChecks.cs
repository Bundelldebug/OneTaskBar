using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class CompactDockChecks
    {
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;DockHost dock=null;int result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{
                    Directory.CreateDirectory(AppLog.Data);Program.Inspectable=true;NowBarLayout.Check();shell=new ShellController(true);shell.Desktop=true;
                    var icon=ArtworkPalette.Sample(Colors.SteelBlue);shell.Pins=Enumerable.Range(1,28).Select(i=>new AppEntry {Name="앱 "+i,Target="fixture:compact:"+i,Icon=icon,Pinned=true}).ToList();
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;dock=new DockHost(shell,screen);dock.Show();await Task.Delay(80);var initial=dock.ShownKeys;
                    shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,Title="좁은 화면에서도 독과 같은 높이에 표시되는 미디어 플레이어",Artist="One TaskBar",Artwork=icon};dock.Refresh(shell.Power);
                    bool aligned=true,clear=true;int samples=0;for(int i=0;i<32;i++){await Task.Delay(18);if(dock.MediaBar==null||!dock.MediaBar.IsVisible)continue;var shape=dock.MediaBar.Shape;Native.RectI apps,start;Native.GetWindowRect(new WindowInteropHelper(dock.Apps).Handle,out apps);Native.GetWindowRect(new WindowInteropHelper(dock.Start).Handle,out start);aligned&=Math.Abs(screen.Bounds.Top+shape.Y*dock.Scale-apps.Top)<=1;clear&=screen.Bounds.Left+shape.Right*dock.Scale<=start.Left-11*dock.Scale;samples++;}
                    bool shown=dock.MediaBar!=null&&dock.MediaBar.IsVisible&&samples>0;bool order=dock.ShownKeys.SequenceEqual(initial.Where(k=>dock.ShownKeys.Contains(k)));bool cap=dock.MediaBar.Shape.Width<=DockLayout.StatusSize(screen.Bounds.Width/dock.Scale,PowerDisplay.Slot(shell.Power,Preferences.Current.DesktopPowerWatts))+.1;
                    double y=dock.MediaBar.Shape.Y;dock.MediaBar.Update(shell.Services.Media,dock.Scale,100,y,true);await Task.Delay(350);bool tiny=Math.Abs(dock.MediaBar.Shape.Y-y)<.01&&dock.MediaBar.Shape.Width==44&&dock.MediaBar.ThumbnailOnly&&dock.MediaBar.Height<=45;dock.MediaBar.SetExpanded(true);await Task.Delay(350);Native.RectI expandedHost,collapsedHost;Native.GetWindowRect(new WindowInteropHelper(dock.MediaBar).Handle,out expandedHost);dock.MediaBar.SetExpanded(false);await Task.Delay(350);Native.GetWindowRect(new WindowInteropHelper(dock.MediaBar).Handle,out collapsedHost);bool returned=Math.Abs(dock.MediaBar.Shape.Y-y)<.01&&dock.MediaBar.Shape.Height==44&&collapsedHost.Equals(expandedHost);
                    dock.Refresh(shell.Power);dock.UpdateVisibility(false,true);dock.CompleteAutoHide(false);await Task.Delay(250);bool mediaHidden=!dock.MediaBar.IsVisible;dock.Reveal();await Task.Delay(35);bool mediaFade=dock.AutoHideAnimating&&dock.MediaBar.IsVisible&&Math.Abs(dock.MediaBar.Opacity-dock.RevealProgress)<.002&&Math.Abs(Native.BlurOpacity(dock.MediaBar)-dock.RevealProgress)<.002;await Task.Delay(290);bool mediaRestored=dock.MediaBar.IsVisible&&dock.MediaBar.Opacity==1&&Native.BlurOpacity(dock.MediaBar)>.999;dock.MediaBar.SetExpanded(true);await Task.Delay(350);bool held=shell.DockInUse(dock);dock.MediaBar.CollapseOutside();await Task.Delay(250);bool dismissed=!dock.DockVisible&&!dock.MediaBar.IsVisible&&!dock.MediaBar.Expanded;dock.UpdateVisibility(false,false);
                    shell.Services.Media=new MediaState();await Task.Delay(950);dock.Refresh(shell.Power);await Task.Delay(400);bool released=!dock.MediaBar.IsVisible&&dock.ShownKeys.SequenceEqual(initial);bool passed=shown&&aligned&&clear&&order&&cap&&tiny&&returned&&released&&mediaHidden&&mediaFade&&mediaRestored&&held&&dismissed;
                    AppLog.Json("compact-dock-probe.json",new {passed=passed,mediaAppearsOnDockBaseline=shown&&aligned,noDockOverlap=clear,existingOrderPreserved=order,cappedByActualStatusWidth=cap,narrowGapNeverRaisesMedia=tiny,expandedReturnUsesSameBaseline=returned,mediaAutoHidesWithDock=mediaHidden,mediaRevealContentAndBlurMatch=mediaFade,mediaReturnsFullyVisible=mediaRestored,expandedMediaPausesAutoHide=held,outsideClickCollapsesAndHides=dismissed,spaceReturnedAfterMediaEnds=released,transitionSamples=samples,syntheticMixedDpiLayouts=360,actualMonitorCount=System.Windows.Forms.Screen.AllScreens.Length,applicationsLaunched=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("compact-dock-probe.json",new {passed=false,error=error.ToString()});}finally{if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
