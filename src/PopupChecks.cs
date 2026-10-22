using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class PopupChecks
    {
        static void Click(Button button){var peer=UIElementAutomationPeer.CreatePeerForElement(button);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();}
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;GlassWindow holder=null;int result=1;
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try {
                    Program.Inspectable=true;shell=new ShellController(true);var screen=System.Windows.Forms.Screen.PrimaryScreen;bool surfaces=true,orphanFree=true;var panels=new List<string>();
                    foreach(var panel in new GlassWindow[]{new QuickWindow(shell),new NotificationWindow(shell),new DetailWindow(shell,"sound"),new StartWindow(shell),new DrawerWindow(shell),new SearchWindow(shell),new NotificationConnectWindow(shell)}){
                        panel.ShowAt(screen.Bounds,30,30,380,540);panel.Pop();for(int i=0;i<12;i++){await Task.Delay(18);surfaces&=panel.Surface.RenderTransform.Value.IsIdentity&&panel.TransitionContent!=null;orphanFree&=LiveBackdrop.OrphanCount==0;}await panel.DismissAsync();await Task.Delay(30);orphanFree&=LiveBackdrop.OrphanCount==0&&LiveBackdrop.VisibleCount==0;panels.Add(panel.Title);panel.Close();
                    }
                    holder=new GlassWindow {Width=350,Height=200};holder.Surface.Child=Paint.TextBlock("Menu fixture",15,Paint.Text);holder.ShowAt(screen.Bounds,40,40,350,200);holder.Activate();var menu=new ContextMenu {PlacementTarget=holder.Surface,Placement=PlacementMode.Center};menu.Items.Add(new MenuItem {Header="바깥 클릭 닫기"});menu.IsOpen=true;await Task.Delay(150);bool hook=MenuDismiss.Active,menuBlur=MenuTheme.HasBackdrop(menu);MenuDismiss.PointerPressed(new Native.PointI {X=screen.Bounds.Left-100,Y=screen.Bounds.Top-100});await Task.Delay(80);bool dismissed=!menu.IsOpen&&!MenuDismiss.Active&&!MenuTheme.HasBackdrop(menu);holder.HideImmediately();orphanFree&=LiveBackdrop.OrphanCount==0&&LiveBackdrop.VisibleCount==0;
                    var commands=new List<string>();var seeks=new List<double>();int outputs=0;var view=new MediaPlayerView(c=>commands.Add(c),v=>{seeks.Add(v);return Task.FromResult(true);},()=>outputs++);var frame=new Border {CornerRadius=new CornerRadius(28),Child=view};var cover=new BitmapImage(new Uri(Path.Combine(AppLog.Root,"assets","OneTaskBar.png")));cover.Freeze();var track=new MediaState {Available=true,Active=true,Playing=true,CanPlay=true,CanNext=true,CanPrevious=true,CanSeek=true,Title="같은 디자인의 미디어 플레이어",Artist="One TaskBar fixture",Duration=200,Position=50,Artwork=cover};view.Update(track,"Galaxy Buds4 Pro");frame.Background=view.MediaBackground;Program.Save(frame,380,188,Path.Combine(AppLog.Data,"shared-media-player.png"));Click(view.PlayButton);Click(view.NextButton);Click(view.PreviousButton);Click(view.OutputButton);var peer=UIElementAutomationPeer.CreatePeerForElement(view.SeekBar);((IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue)).SetValue(65);await Task.Delay(90);bool media=commands.SequenceEqual(new[]{"play","next","previous"})&&seeks.SequenceEqual(new[]{65.0})&&outputs==1&&view.OutputName=="Galaxy Buds4 Pro";
                    var slim=new MediaPlayerView(c=>{},v=>Task.FromResult(true),()=>{},condensed:true);slim.Update(track,"Galaxy Buds4 Pro");var slimFrame=new Border {CornerRadius=new CornerRadius(28),Background=slim.MediaBackground,Child=slim};Program.Save(slimFrame,344,140,Path.Combine(AppLog.Data,"slim-media-player.png"));bool frozenReuse=view.MediaBackground.IsFrozen&&Object.ReferenceEquals(slim.MediaBackground,view.MediaBackground);for(int i=0;i<100;i++){slim.Update(track,"Galaxy Buds4 Pro");frozenReuse&=Object.ReferenceEquals(slim.MediaBackground,view.MediaBackground);}var controlBounds=slim.PlayButton.TransformToAncestor(slimFrame).TransformBounds(new Rect(0,0,slim.PlayButton.ActualWidth,slim.PlayButton.ActualHeight));bool slimFits=slimFrame.ActualHeight==140&&controlBounds.Bottom<=slimFrame.ActualHeight&&slim.SeekBar.ActualWidth>200;
                    shell.Services.Media=track;var quickPreview=new QuickWindow(shell);quickPreview.Refresh(new PowerState {Known=true,Level=89,Connected=false},50,35,false,true,true);Program.Save(quickPreview.Surface,380,650,Path.Combine(AppLog.Data,"quick-media-player.png"));
                    bool oldScroll=Preferences.Current.QuickSliderScroll;Preferences.Current.QuickSliderScroll=false;slim.Update(track,"Galaxy Buds4 Pro");double beforeScroll=slim.SeekBar.Value;var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,12){RoutedEvent=Mouse.MouseWheelEvent};slim.SeekBar.RaiseEvent(wheel);bool wheelDefaultOff=!new PreferenceData().QuickSliderScroll&&!wheel.Handled&&slim.SeekBar.Value==beforeScroll;
                    var toggle=quickPreview.FindToggle("slider-scroll");var togglePeer=UIElementAutomationPeer.CreatePeerForElement(toggle);((IToggleProvider)togglePeer.GetPattern(PatternInterface.Toggle)).Toggle();slim.Update(track,"Galaxy Buds4 Pro");var enabledWheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=Mouse.MouseWheelEvent};slim.SeekBar.RaiseEvent(enabledWheel);bool wheelToggle=Preferences.Current.QuickSliderScroll&&toggle.Checked&&enabledWheel.Handled&&slim.SeekBar.Value>beforeScroll;Preferences.Set(p=>p.QuickSliderScroll=oldScroll);
                    quickPreview.Close();shell.Services.Media=new MediaState();view.Update(new MediaState(),"스피커");frame.Background=QuickPanel.Card;Program.Save(frame,380,62,Path.Combine(AppLog.Data,"idle-media-player.png"));bool compact=view.IsCompact&&view.Height==48&&view.Margin.Top+view.Margin.Bottom==14;
                    var wireless=new StackPanel {Background=Paint.Brush("#FFF1F2F3")};foreach(int signal in new[]{0,15,50,99}){var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(12)};row.Children.Add(new Glyph("wifi",48){Color=Paint.Ink,Generation=7,Signal=signal,Traffic=3});row.Children.Add(Paint.TextBlock("신호 "+signal,14,Paint.Ink));wireless.Children.Add(row);}Program.Save(wireless,260,288,Path.Combine(AppLog.Data,"wifi-signal-states.png"));bool wifi=WifiLink.Bars(0)==0&&WifiLink.Bars(15)==1&&WifiLink.Bars(50)==2&&WifiLink.Bars(99)==3;
                    var devices=Native.AudioOutputs();result=surfaces&&orphanFree&&hook&&menuBlur&&dismissed&&media&&compact&&slimFits&&frozenReuse&&wheelDefaultOff&&wheelToggle&&wifi?0:1;AppLog.Json("popup-probe.json",new {passed=result==0,sliderWheelDefaultOffAndBubbles=wheelDefaultOff,sliderWheelToggleWorks=wheelToggle,slimPlayerHeight=slimFrame.ActualHeight,slimControlsFit=slimFits,artworkSurfaceFrozenAndReused=frozenReuse,panels=panels.ToArray(),stationaryMatchingSurfaceAndBlur=surfaces,noOrphanBackdrops=orphanFree,outsideClickObserverInstalled=hook,outsideClickClosesAndReleasesMenu=dismissed,menuBlur=menuBlur,sharedMediaControls=media,mediaCommands=commands.ToArray(),seekValues=seeks.ToArray(),outputButtonCalls=outputs,compactIdleMedia=compact,wifiSignalStates=wifi,defaultOutputRead=devices.Any(d=>d.Current),realPlayersOrDevicesChanged=false});
                }catch(Exception error){AppLog.Json("popup-probe.json",new {passed=false,error=error.ToString()});}
                finally{if(holder!=null)holder.Close();if(shell!=null)shell.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
