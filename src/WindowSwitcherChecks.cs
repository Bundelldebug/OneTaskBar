using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class WindowSwitcherChecks
    {
        static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
        internal static int Run(bool inspect)
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            var sources=new List<Window>();var items=new List<SwitchItem>();var activated=new List<IntPtr>();
            ShellController shell=null;WindowSwitcherManager manager=null;IDisposable pointer=null;int result=1;bool oldNative=Preferences.Current.NativeAltTab;
            application.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;pointer=MenuDismiss.WithSyntheticPointerForFixture();Preferences.Current.NativeAltTab=false;WindowSwitchGesture.Check();shell=new ShellController(true);
                foreach(var color in new[]{Colors.SteelBlue,Colors.SeaGreen,Colors.IndianRed,Colors.SlateBlue,Colors.DarkGoldenrod,Colors.DarkCyan,Colors.Sienna}){
                    int index=sources.Count;var surface=new Grid {Background=new SolidColorBrush(color)};surface.RowDefinitions.Add(new RowDefinition());surface.RowDefinitions.Add(new RowDefinition {Height=new GridLength(58)});
                    surface.Children.Add(new TextBlock {Text=new[]{"검색","파일","미디어","메모","사진","브라우저","설정"}[index],Foreground=Brushes.White,FontSize=44,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
                    var footer=new Border {Background=Paint.Brush("#33000000"),Child=new TextBlock {Text="실시간 창 미리보기 · "+(index+1),Margin=new Thickness(18),Foreground=Brushes.White,FontSize=15}};Grid.SetRow(footer,1);surface.Children.Add(footer);
                    var source=new Window {Title="창 전환 검사 · "+(index+1),Width=500,Height=330,Left=50+index*30,Top=320+index*18,ShowInTaskbar=false,Content=surface};sources.Add(source);source.Show();
                    var entry=new AppEntry {Name=new[]{"검색","파일 탐색기","미디어 플레이어","메모","사진","브라우저","One Taskbar"}[index],Target="fixture:switch:"+index,Icon=ArtworkPalette.Sample(color)};var handle=new WindowInteropHelper(source).Handle;entry.Windows.Add(handle);items.Add(new SwitchItem {Window=handle,Title=source.Title,App=entry});
                }
                using(var child=new HwndSource(new HwndSourceParameters("Hosted foreground fixture") {ParentWindow=items[0].Window,WindowStyle=0x50000000,Width=30,Height=30}))
                {
                    Require(Native.GetAncestor(child.Handle,2)==items[0].Window,"fixture child belongs to its outer frame");
                    Require(new SwitchSession(items,child.Handle,1).Current.Window==items[1].Window&&new SwitchSession(items,child.Handle,-1).Current.Window==items[items.Count-1].Window,"hosted foreground advances from the frame in both directions, rather than reselecting itself");
                }
                var available=items.Take(3).ToList();manager=new WindowSwitcherManager(shell,()=>available.ToList(),h=>{activated.Add(h);Native.Activate(h);});manager.Update(new AppEntry[0]);sources[0].Activate();await Task.Delay(70);IntPtr original=Native.GetForegroundWindow();
                var gesture=new WindowSwitchGesture {Cycle=manager.Cycle,Navigate=manager.Navigate,Finish=manager.Finish,AltReleased=manager.AltReleased};
                Require(gesture.Observe(9,true,true,true,false,false,false,false)&&manager.View==null,"Alt+Tab must queue UI work outside the keyboard hook");gesture.Observe(9,false,true,true,false,false,false,false);await Task.Delay(180);
                var view=manager.View;int first=available.FindIndex(i=>i.Window==original),expected=first<0?0:(first+1)%3;
                Require(view!=null&&view.IsVisible&&view.VisibleCards==3&&view.RegisteredThumbnails==3&&manager.Selected==expected,"Three live source previews and initial MRU selection");
                Require(Native.GetForegroundWindow()==original,"Preview windows must not steal keyboard focus");
                var hosts=application.Windows.OfType<SwitchThumbnail>().ToArray();Require(hosts.Length==3&&hosts.All(w=>{int styles=Native.GetWindowLong(new WindowInteropHelper(w).Handle,-20);return (styles&0x80)!=0&&(styles&0x08000000)!=0&&(styles&0x40000)==0;}),"Native previews stay outside Alt+Tab and do not activate");
                Require(view.PreviewBounds.All(b=>b.Left>=0&&b.Top>=0&&b.Right<=view.ActualWidth+1&&b.Bottom<=view.ActualHeight+1),"Live preview slots fit inside the glass panel");
                Program.Save(view.Surface,(int)view.Width,(int)view.Height,Path.Combine(AppLog.Data,"window-switcher-layout.png"));
                if(inspect){var inspectHandle=new WindowInteropHelper(view).Handle;int originalStyles=Native.GetWindowLong(inspectHandle,-20);view.ShowInTaskbar=true;Native.SetWindowLong(inspectHandle,-20,(originalStyles|0x40000)&~0x80&~0x08000000);await Task.Delay(22000);view.ShowInTaskbar=false;Native.SetWindowLong(inspectHandle,-20,originalStyles);}
                gesture.Observe(9,true,true,true,false,true,false,false);gesture.Observe(9,false,true,true,false,true,false,false);await Task.Delay(45);Require(manager.Selected==(expected+2)%3,"Shift+Tab cycles backward");
                gesture.Observe(0x24,true,true,true,false,false,false,false);gesture.Observe(0x24,false,true,true,false,false,false,false);await Task.Delay(40);Require(manager.Selected==0,"Home selects first window");
                gesture.Observe(0x23,true,true,true,false,false,false,false);gesture.Observe(0x23,false,true,true,false,false,false,false);await Task.Delay(40);Require(manager.Selected==2,"End selects last window");
                hosts[0].RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=UIElement.MouseWheelEvent});await Task.Delay(40);Require(manager.Selected==1,"Mouse wheel over a native preview selects the adjacent window");
                gesture.Observe(0xA4,false,true,false,false,false,false,false);await Task.Delay(100);Require(activated.SequenceEqual(new[]{items[1].Window})&&!manager.Active&&manager.View==null&&!manager.TimerActive&&view.RegisteredThumbnails==0&&hosts.All(w=>!w.IsVisible),"Releasing Alt commits exactly once and releases all previews and timers");
                int activationCount=activated.Count;Require(manager.Cycle(false),"Start another switch");await Task.Delay(60);manager.Finish(false);await Task.Delay(60);Require(activated.Count==activationCount&&!manager.Active&&!manager.TimerActive,"Escape cancellation never activates another window");
                Require(manager.Cycle(false)&&manager.View==null,"Fast Alt+Tab is deferred");manager.Finish(true);await Task.Delay(80);Require(activated.Count==activationCount+1&&manager.View==null&&!manager.TimerActive,"Fast key release commits without flashing an empty popup");
                available=items.ToList();manager.Update(new AppEntry[0]);Require(manager.Cycle(false),"Open seven-window session");await Task.Delay(80);manager.Navigate(0x23);await Task.Delay(80);Require(manager.Selected==6&&manager.View.VisibleCards==5&&manager.View.PageStart==2&&manager.View.RegisteredThumbnails==5,"More than five windows page without losing the selected item");
                sources[6].Close();available.RemoveAt(6);await Task.Delay(180);Require(manager.Selected==5&&manager.View.VisibleCards==5&&manager.View.RegisteredThumbnails==5,"Closing the selected window prunes the session and replaces its native preview");
                var clickable=application.Windows.OfType<SwitchThumbnail>().First();activationCount=activated.Count;clickable.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});await Task.Delay(70);Require(activated.Count==activationCount+1&&!manager.Active,"Clicking a preview activates its source and closes the panel");
                for(int i=0;i<5;i++){manager.Cycle(false);await Task.Delay(35);manager.Navigate(0x23);await Task.Delay(35);manager.Cancel();await Task.Delay(35);Require(!manager.TimerActive&&!application.Windows.OfType<SwitchThumbnail>().Any(),"Repeated sessions release every native preview");}
                manager.Cycle(false);await Task.Delay(50);Preferences.Current.NativeAltTab=true;manager.PreferenceChanged();Require(!manager.Cycle(false)&&!manager.Active&&manager.View==null,"Windows native choice immediately cancels and passes future shortcuts through");Preferences.Current.NativeAltTab=false;manager.PreferenceChanged();Require(manager.Cycle(false),"Re-enabling custom switching refreshes its window list");manager.Cancel();
                available=available.Take(1).ToList();manager.Update(new AppEntry[0]);Require(manager.Cycle(false),"A single window must keep the custom Alt+Tab UI");await Task.Delay(70);var single=manager.View;Require(single!=null&&single.IsVisible&&single.VisibleCards==1&&single.RegisteredThumbnails==1&&manager.Selected==0,"A single live preview is displayed and selected");int presentations=single.Presentations;for(int i=0;i<20;i++){manager.Cycle(i%2==0);manager.Navigate(i%2==0?0x24:0x23);}await Task.Delay(70);Require(manager.View==single&&single.Presentations==presentations&&single.RegisteredThumbnails==1&&manager.Selected==0,"Unchanged single-window selection reuses its layout and native preview");activationCount=activated.Count;manager.Finish(true);await Task.Delay(60);Require(activated.Count==activationCount+1&&activated.Last()==available[0].Window&&!manager.Active&&single.RegisteredThumbnails==0,"Releasing Alt on the only window commits and cleans up");
                manager.Cycle(true);await Task.Delay(60);sources[0].Close();available.Clear();await Task.Delay(170);Require(!manager.Active&&manager.View==null&&!manager.TimerActive,"Closing the only source cancels the chooser without activating a stale HWND");manager.Update(new AppEntry[0]);Require(!manager.Cycle(false),"An empty eligible window list still falls through to Windows");
                await Task.Delay(100);Require(!MenuDismiss.Active&&!manager.TimerActive&&LiveBackdrop.OrphanCount==0&&!application.Windows.OfType<SwitchThumbnail>().Any()&&!application.Windows.OfType<WindowSwitcherView>().Any(),"No input observer, timer, native preview or backdrop survives dismissal");
                AppLog.Json("window-switcher-probe.json",new {passed=true,threeLiveDwmPreviews=true,singleWindowCustomUi=true,singleWindowLayoutReused=true,singleWindowCloseCleansUp=true,focusPreserved=true,keyboardHookUiDeferred=true,reverseAndHomeEnd=true,altReleaseCommitsOnce=true,escapeCancels=true,fastReleaseWithoutPopup=true,mouseWheelOnPreview=true,clickActivatesSource=true,sevenWindowPaging=true,closedWindowPruned=true,nativeChoicePersists=true,resourcesReleased=true,fixtureWindowsOnly=true,osInputSent=false});result=0;
            }catch(Exception error){AppLog.Json("window-switcher-probe.json",new {passed=false,error=error.ToString(),selected=manager==null?-1:manager.Selected,visibleCards=manager==null||manager.View==null?0:manager.View.VisibleCards,registered=manager==null||manager.View==null?0:manager.View.RegisteredThumbnails});}finally{
                if(manager!=null)manager.Dispose();if(shell!=null)shell.Dispose();foreach(var source in sources)if(new WindowInteropHelper(source).Handle!=IntPtr.Zero&&Native.IsWindow(new WindowInteropHelper(source).Handle))source.Close();Preferences.Current.NativeAltTab=oldNative;if(pointer!=null)pointer.Dispose();application.Shutdown(result);
            }}),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
