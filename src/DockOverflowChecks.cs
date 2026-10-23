using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class DockOverflowChecks
    {
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;DockHost dock=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try{Program.Inspectable=true;Directory.CreateDirectory(AppLog.Data);shell=new ShellController(true);shell.Desktop=true;var icon=ArtworkPalette.Sample(Colors.SteelBlue);shell.Pins=Enumerable.Range(1,4).Select(i=>new AppEntry {Name="고정 앱 "+i,Target="fixture:pin:"+i,Icon=icon,Pinned=true}).ToList();dock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);dock.Show();await Task.Delay(100);var pins=dock.ShownKeys.ToArray();shell.Running=Enumerable.Range(1,80).Select(i=>new AppEntry {Name="실행 앱 "+i,Target="fixture:run:"+i,Icon=icon}).ToList();dock.Rebuild();await Task.Delay(390);bool preserved=pins.SequenceEqual(dock.ShownKeys.Take(pins.Length));bool covered=dock.Overflow.Count+dock.ShownKeys.Length==84&&dock.OverflowButton.IsVisible&&Canvas.GetLeft(dock.OverflowButton)+dock.OverflowButton.ActualWidth<=dock.Apps.ActualWidth;dock.OverflowButton.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(380);bool hover=dock.Overflow.IsOpen;bool scroll=hover&&dock.Overflow.Popup.Height<=276&&dock.Overflow.Popup.HasBlur;if(hover)Program.Save(dock.Overflow.Popup.Surface,(int)dock.Overflow.Popup.Width,(int)dock.Overflow.Popup.Height,Path.Combine(AppLog.Data,"dock-overflow.png"));MenuDismiss.PointerPressed(new Native.PointI {X=dock.Screen.Bounds.Left+1,Y=dock.Screen.Bounds.Top+1});await Task.Delay(250);bool outside=!dock.Overflow.IsOpen&&LiveBackdrop.OrphanCount==0;shell.Running.Clear();dock.Rebuild();await Task.Delay(400);bool restores=!dock.OverflowButton.IsVisible&&dock.Overflow.Count==0&&pins.SequenceEqual(dock.ShownKeys);bool passed=preserved&&covered&&hover&&scroll&&outside&&restores;AppLog.Json("dock-overflow-probe.json",new {passed=passed,earlyPinsRemainVisible=preserved,everyAppVisibleOrInOverflow=covered,hoverHandlerOpens=hover,scrollablePopupWithinHeightLimit=scroll,outsideClickDismisses=outside,overflowRemovedWhenSpaceReturns=restores,fixtureRunningApps=80,realAppsOpened=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("dock-overflow-probe.json",new {passed=false,error=error.ToString()});}finally{if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
