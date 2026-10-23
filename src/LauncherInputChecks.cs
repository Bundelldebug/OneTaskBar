using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    internal static class LauncherInputChecks
    {
        internal static void Validate(List<string> checks)
        {
            MediaActivationChecks.Validate(checks);
            var app=new AppEntry {Name="Desktop",Target=@"C:\Apps\Tool.lnk",Executable=@"C:\Apps\Tool.exe"};app.Windows.Add(new IntPtr(123));var info=AdminLaunch.Prepare(app);
            if(info==null||info.FileName!=app.Target||info.Verb!="runas"||!info.UseShellExecute||app.Windows.Count!=1)throw new Exception("Admin launch lost shortcut semantics or altered existing windows");
            if(AdminLaunch.Prepare(new AppEntry {AppID="Package!App",Target=@"C:\Apps\Tool.exe"})!=null||AdminLaunch.Prepare(new AppEntry {Target="custom:tool.exe"})!=null||AdminLaunch.Prepare(new AppEntry {Target="https://example.com/tool.exe"})!=null||AdminLaunch.Prepare(new AppEntry {Target="ms-settings:"})!=null)throw new Exception("Unsupported app exposed elevation");
            if(AdminLaunch.Prepare(new AppEntry {AppID="Desktop.App",Executable=@"C:\Apps\Tool.exe"})==null)throw new Exception("Running desktop executable cannot elevate");
            checks.Add("Admin launch preserves shortcut arguments through ShellExecute/runas, opens a new process and excludes packaged apps/URIs without invoking UAC in tests");
            string id="AbC_123-xYz";
            if(YouTubeArtwork.VideoId("https://www.youtube.com/watch?v="+id+"&list=test")!=id||YouTubeArtwork.VideoId("https://youtu.be/"+id)!=id||YouTubeArtwork.VideoId("https://youtube.com.attacker.test/watch?v="+id)!=null||YouTubeArtwork.VideoId("https://www.youtube.com/watch?v=invalid")!=null||YouTubeArtwork.Profile("Spotify.exe")!=null||YouTubeArtwork.Profile("chrome.exe")!=null)throw new Exception("Artwork source or exact video validation");
            bool saved=Preferences.Current.YouTubeHighResArtwork;string scope=Preferences.Current.YouTubeArtworkTrack;
            try{Preferences.Current.YouTubeHighResArtwork=false;Preferences.Current.YouTubeArtworkTrack=YouTubeArtwork.TrackKey("Internet","Only this video");if(!YouTubeArtwork.Allowed("Internet","Only this video")||YouTubeArtwork.Allowed("Internet","Another video")||YouTubeArtwork.Allowed("Spotify.exe","Only this video"))throw new Exception("Single-video consent escaped its scope");}finally{Preferences.Current.YouTubeHighResArtwork=saved;Preferences.Current.YouTubeArtworkTrack=scope;}
            if(new PreferenceData().YouTubeHighResArtwork)throw new Exception("Browser history opt-in enabled by default");checks.Add("YouTube artwork defaults off, supports a single-video consent hash and never resolves another browser/app or sends titles to the CDN");
        }
        static bool Press(UIElement target,Key key)
        {
            var args=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(target),Environment.TickCount,key){RoutedEvent=Keyboard.PreviewKeyDownEvent};target.RaiseEvent(args);return args.Handled;
        }
        static bool Admin(ContextMenu menu){return menu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="관리자 권한으로 실행");}
        internal static int Run()
        {
            var application=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;ShellController shell=null;DockHost dock=null;SearchWindow finder=null;DrawerWindow drawer=null;StartWindow start=null;Window fixture=null;
            application.Dispatcher.BeginInvoke(new Action(()=> {
                try{
                    Program.Inspectable=true;shell=new ShellController(true);shell.Apps=Enumerable.Range(1,8).Select(i=>new AppEntry {Name="Fixture "+i,Target="fixture:"+i}).ToList();dock=new DockHost(shell,System.Windows.Forms.Screen.PrimaryScreen);dock.Show();dock.TaskViewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));bool taskView=shell.TaskViewRequests==1&&Canvas.GetLeft(dock.TaskViewButton)==52&&DockLayout.AppOffset>=Canvas.GetLeft(dock.TaskViewButton)+dock.TaskViewButton.Width;
                    shell.Apps[0].Target=@"C:\Apps\Fixture.exe";var exeMenu=WindowPicker.Menu(shell.Apps[0],shell,allowAdmin:true);var packageMenu=WindowPicker.Menu(new AppEntry {AppID="Fixture.Package!App"},shell,allowAdmin:true);bool admin=Admin(exeMenu)&&Admin(packageMenu)&&!packageMenu.Items.OfType<MenuItem>().First(m=>Convert.ToString(m.Header)=="관리자 권한으로 실행").IsEnabled&&exeMenu.Items.OfType<MenuItem>().Any(m=>Convert.ToString(m.Header)=="앱 제거…");bool noDockAdmin=!Admin(WindowPicker.Menu(shell.Apps[0],shell));
                    drawer=new DrawerWindow(shell);drawer.Configure(720,650);drawer.Reload();bool appMenu=Admin(drawer.AppButton(shell.Apps[0].Key).ContextMenu);shell.StartPins.Replace(shell.Apps);start=new StartWindow(shell);start.FixturePhone(new PhoneSnapshot());start.FixtureRecent(new List<StartRecent>());start.Reload();bool startMenu=Admin(start.PinButton(shell.Apps[0].Key).ContextMenu);
                    finder=new SearchWindow(shell);finder.Configure(720,680);finder.Reload();finder.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,20,20,720,680);finder.Surface.UpdateLayout();finder.Query.Focus();bool wired=Press(finder.Query,Key.Down)&&finder.Navigation.Selected!=null&&finder.Navigation.Count==14;bool searchMenu=finder.Navigation.Selected!=null&&Admin(finder.Navigation.Selected.ContextMenu);finder.Query.Text="no-match-fixture-938471";bool cleared=finder.Navigation.Selected==null;finder.HideImmediately();
                    int opened=-1;var root=new StackPanel();var query=new TextBox {Height=35};root.Children.Add(query);var grid=new UniformGrid {Columns=3};root.Children.Add(grid);fixture=new Window {Content=root,Width=350,Height=240,ShowInTaskbar=false,Title="One TaskBar · 검색 키보드 검사"};var nav=new SearchNavigation(fixture,query);var buttons=new List<Button>();
                    for(int i=0;i<6;i++){int index=i;var b=Paint.Action("결과 "+i,()=>opened=index);b.Height=55;nav.Add(b,()=>opened=index);buttons.Add(b);grid.Children.Add(b);}fixture.Show();fixture.Activate();fixture.UpdateLayout();query.Focus();Press(query,Key.Down);bool down=nav.Selected==buttons[0]&&buttons[0].IsKeyboardFocused&&buttons[0].BorderBrush!=Brushes.Transparent;double width=buttons[0].ActualWidth;
                    down=down&&buttons[0].FocusVisualStyle==null;Press(buttons[0],Key.Right);bool right=nav.Selected==buttons[1];Press(buttons[1],Key.Down);bool row=nav.Selected==buttons[4];Press(buttons[4],Key.Left);bool left=nav.Selected==buttons[3];Press(buttons[3],Key.Up);bool up=nav.Selected==buttons[0];Press(buttons[0],Key.Enter);bool enter=opened==0;Press(buttons[0],Key.Up);bool returnToQuery=query.IsKeyboardFocused&&nav.Selected==null;opened=-1;Press(query,Key.Enter);bool defaultEnter=opened==0;
                    opened=-1;var composition=new TextComposition(InputManager.Current,query,"한");query.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputStartEvent});bool imeStart=nav.Composing;bool imeEnter=Press(query,Key.Enter);query.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,composition){RoutedEvent=TextCompositionManager.PreviewTextInputEvent});bool ime=imeStart&&!imeEnter&&opened==-1&&!nav.Composing;bool caret=!Press(query,Key.Left);nav.Clear();bool release=nav.Selected==null&&nav.Count==0&&buttons[0].ActualWidth==width;
                    bool passed=taskView&&admin&&noDockAdmin&&appMenu&&startMenu&&searchMenu&&wired&&cleared&&down&&right&&row&&left&&up&&enter&&returnToQuery&&defaultEnter&&ime&&caret&&release;AppLog.Json("launcher-keyboard-probe.json",new {passed=passed,taskViewButtonPlacementAndDispatch=taskView,adminOnlyForDesktopApps=admin,adminInAppScreen=appMenu,adminInSearch=searchMenu,adminInStart=startMenu,adminAbsentFromDock=noDockAdmin,searchWindowNavigationWired=wired,queryChangeClearsSelection=cleared,downSelectsResult=down,rightMovesAcross=right,downMovesRow=row,leftMovesAcross=left,upMovesRow=up,enterInvokesSelected=enter,upReturnsToQuery=returnToQuery,queryEnterInvokesFirst=defaultEnter,koreanCompositionDoesNotLaunch=ime,queryCaretPreserved=caret,clearReleasesResultsWithoutLayoutJump=release,systemTaskViewOpened=false,uacInvoked=false,externalAppsLaunched=false});result=passed?0:1;
                }catch(Exception error){AppLog.Json("launcher-keyboard-probe.json",new {passed=false,error=error.ToString()});}finally{if(fixture!=null)fixture.Close();if(finder!=null)finder.Close();if(drawer!=null)drawer.Close();if(start!=null)start.Close();if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();application.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);return application.Run();
        }
    }
}
