using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Dex desktop style in the real windows, over a wallpaper like the recording's, with frames saved for comparison.
    internal static class DexChecks
    {
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        static bool screenCaptureFailed;
        static void Shot(string name,GlassWindow owned=null)
        {
            if(owned!=null&&owned.Surface!=null&&owned.ActualWidth>4&&owned.ActualHeight>4)
            {
                try{Program.Save(owned.Surface,(int)Math.Ceiling(owned.ActualWidth),(int)Math.Ceiling(owned.ActualHeight),Path.Combine(AppLog.Data,"dex-own-"+name+".png"));}
                catch(Exception){}
            }
            if(screenCaptureFailed)return;
            try
            {
                var area=System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using(var bitmap=new System.Drawing.Bitmap(area.Width,area.Height))
                {
                    using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(area.X,area.Y,0,0,bitmap.Size);
                    bitmap.Save(Path.Combine(AppLog.Data,"dex-"+name+".png"),System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch(System.ComponentModel.Win32Exception){screenCaptureFailed=true;}
        }
        static Native.RectI Bounds(Window window){Native.RectI rect;Native.GetWindowRect(new WindowInteropHelper(window).Handle,out rect);return rect;}
        static async Task Frames(string name,params int[] times)
        {
            int waited=0;
            foreach(int at in times){await Task.Delay(Math.Max(0,at-waited));waited=at;Shot(name+"-"+at);}
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;IDisposable motion=null,palette=null;ShellController shell=null;DockHost dock=null;
            var extras=new List<Window>();
            app.Dispatcher.BeginInvoke(new Action(async()=>{
                try
                {
                    Program.Inspectable=true;
                    var report=new List<string>();
                    Preferences.Set(p=>{p.UIVersion=95;p.DexDesktop=true;p.TopStatusBar=true;p.DynamicIsland=false;p.DuoStatusIcon=false;p.ShowNowBar=true;p.NativeSearch=false;p.NativeStartButton=false;p.NativeQuickSettings=false;p.NativeNotifications=false;p.DockEdge=0;});
                    Require(Dex.Enabled,"the style is on");
                    Require(DockGeometry.Edge==3,"the Dex bar is always at the bottom");
                    Require(WelcomeWindow.CurrentDesktopStyle()==2,"the settings card shows the Dex style as chosen");
                    motion=Motion.WithAnimationsForFixture();palette=Glass95.WithDarkForFixture(false);
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    var wall=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
                    wall.GradientStops.Add(new GradientStop(Color.FromRgb(0xB5,0xDC,0xE2),0));wall.GradientStops.Add(new GradientStop(Color.FromRgb(0xF3,0xD8,0xD2),.5));wall.GradientStops.Add(new GradientStop(Color.FromRgb(0x8F,0xBC,0xD8),1));
                    var backdrop=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Topmost=true,ShowActivated=false,Background=wall};
                    extras.Add(backdrop);new WindowInteropHelper(backdrop).EnsureHandle();backdrop.Show();
                    Native.SetWindowPos(new WindowInteropHelper(backdrop).Handle,new IntPtr(-1),screen.Bounds.Left,screen.Bounds.Top,screen.Bounds.Width,screen.Bounds.Height,0x10);
                    shell=new ShellController(true);shell.Desktop=true;
                    shell.Apps=await BackgroundWork.Run(()=>Catalog.Read(false));
                    shell.Pins=shell.Apps.Take(5).Select(a=>a.Copy()).ToList();foreach(var pin in shell.Pins)pin.Pinned=true;
                    shell.Services.Media=new MediaState {Available=true,Active=true,Playing=true,Title="Morfonica Sunny, Sunny",Artist="검사"};
                    dock=shell.DexFixture(screen);
                    await Task.Delay(500);
                    double scale=dock.Scale,wide=screen.Bounds.Width/scale,tall=screen.Bounds.Height/scale;
                    // The bar.
                    var bar=Bounds(dock.Apps);
                    Require(Math.Abs(bar.Left-screen.Bounds.Left)<3&&Math.Abs(bar.Right-screen.Bounds.Right)<3&&Math.Abs(bar.Bottom-screen.Bounds.Bottom)<3,"the bar spans the whole bottom edge: "+bar.Left+","+bar.Right+","+bar.Bottom);
                    Require(Math.Abs((bar.Bottom-bar.Top)/scale-Dex.BarHeight)<1.1,"the bar is as thick as the recording's");
                    Require(!dock.Start.IsVisible&&!dock.Status.IsVisible,"the separate start piece and status capsule are gone");
                    var pill=dock.SearchButton.PointToScreen(new Point(0,0));var keys=dock.ShownKeys;
                    Require(keys.Length==5,"the pinned apps are on the bar: "+keys.Length);
                    var last=dock.AppButton(keys[4]);var end=last.PointToScreen(new Point(last.ActualWidth,0));
                    double center=((pill.X+end.X)/2-screen.Bounds.Left)/scale;
                    Require(Math.Abs(center-wide/2)<14,"the pill and apps are centered as one group: "+center+" of "+wide);
                    var first=dock.AppButton(keys[0]);var second=dock.AppButton(keys[1]);
                    Require(Math.Abs((second.PointToScreen(new Point()).X-first.PointToScreen(new Point()).X)/scale-Dex.Pitch)<1,"apps sit at the recording's pitch");
                    report.Add("bar ok");
                    Require(shell.TopBarActive,"the status row is up");
                    Shot("home");
                    // The app grid grows out of its key and stays over the bar.
                    shell.ToggleDrawer(dock);
                    Require(shell.Drawer.IsVisible&&PanelMorph.Running(shell.Drawer),"the app grid opens growing");
                    await Frames("drawer",20,70,130,200,300);await Task.Delay(400);
                    Require(!PanelMorph.Running(shell.Drawer)&&!shell.Drawer.BackdropClip.HasValue,"the app grid ends as the plain panel");
                    var drawer=Bounds(shell.Drawer);var wanted=Dex.DrawerRect(wide,tall);
                    Require(Math.Abs((drawer.Right-drawer.Left)/scale-wanted.Width)<2&&Math.Abs((drawer.Left-screen.Bounds.Left)/scale-wanted.X)<2,"the app grid is centered");
                    Require(dock.SearchButton.Visibility==Visibility.Visible&&dock.Apps.Opacity>.99,"the bar is untouched while the grid is open");
                    Shot("drawer-open",shell.Drawer);shell.Drawer.Hide();Require(PanelMorph.Running(shell.Drawer),"the app grid leaves on the same glass");await Frames("drawer-close",40,100);await Task.Delay(200);Require(!shell.Drawer.IsVisible&&!PanelMorph.Running(shell.Drawer),"the app grid is gone");report.Add("drawer ok");
                    // It is still a taskbar: the Windows key and the start button open the Start menu.
                    shell.StartPins.Replace(shell.Apps.Take(18));shell.StartMenu=new StartWindow(shell);
                    shell.ToggleStart(dock);
                    Require(shell.StartMenu.IsVisible&&!shell.Drawer.IsVisible,"Start opens the Start menu");
                    Require(PanelMorph.Running(shell.StartMenu),"Start grows out of its button");
                    await Task.Delay(800);Shot("start-open",shell.StartMenu);shell.StartMenu.HideImmediately();await Task.Delay(120);report.Add("start ok");
                    // The finder stands over its pill.
                    shell.ToggleSearch(dock);
                    Require(shell.Finder.IsVisible&&PanelMorph.Running(shell.Finder),"the finder fades in");
                    await Frames("finder",40,120,220);await Task.Delay(500);
                    Require(!PanelMorph.Running(shell.Finder),"the finder lands");
                    Shot("finder-open",shell.Finder);shell.Finder.HideImmediately();await Task.Delay(120);report.Add("finder ok");
                    // Quick settings and notifications drop from the top right, out of their capsules.
                    shell.ToggleQuick(dock,true,true);await Task.Delay(30);
                    Require(shell.Quick.IsVisible&&shell.Quick.PopFromTop,"quick settings drop from the status row");
                    await Frames("quick",40,120,220);await Task.Delay(500);
                    var quick=Bounds(shell.Quick);Require((screen.Bounds.Right-quick.Right)/scale<12&&(quick.Top-screen.Bounds.Top)/scale<IslandSafeArea.Height+10,"quick settings stand at the top right");
                    Shot("quick-open",shell.Quick);shell.Quick.HideImmediately();await Task.Delay(120);
                    shell.ToggleNotifications(dock,true);await Task.Delay(500);await Task.Delay(500);
                    var center2=Bounds(shell.Center);Require(shell.Center.IsVisible&&(screen.Bounds.Right-center2.Right)/scale<12,"the notification center stands at the top right");
                    Shot("notifications-open",shell.Center);shell.Center.HideImmediately();await Task.Delay(120);report.Add("panels ok");
                    // The date opens the calendar under it.
                    shell.ToggleCalendar(shell.DexClockBar);await Task.Delay(500);
                    var calendar=Bounds(shell.Calendar);Require(shell.Calendar.IsVisible&&(calendar.Left-screen.Bounds.Left)/scale<24&&(calendar.Top-screen.Bounds.Top)/scale<IslandSafeArea.Height+12,"the calendar opens under the date");
                    Shot("calendar",shell.Calendar);
                    var shownMonth=shell.Calendar.Month;shell.Calendar.MoveMonth(1);await Frames("calendar-slide",40,110);await Task.Delay(300);Require(shell.Calendar.Month.Month==shownMonth.AddMonths(1).Month&&shell.Calendar.DayCells==42,"the next month slides in");
                    shell.Calendar.PickForFixture(true);await Frames("calendar-pick",60,160);await Task.Delay(300);var picking=Bounds(shell.Calendar);Require(shell.Calendar.Picking&&Math.Abs((picking.Bottom-picking.Top)/scale-CalendarWindow.DexPickerHeight)<2&&!PanelMorph.Running(shell.Calendar),"the panel grows to hold the wheels");Shot("calendar-wheels",shell.Calendar);
                    shell.Calendar.PickForFixture(false);await Task.Delay(420);shell.Calendar.Hide();Require(PanelMorph.Running(shell.Calendar),"the calendar leaves on the same glass");await Task.Delay(320);Require(!shell.Calendar.IsVisible,"the calendar is gone");report.Add("calendar ok");
                    // Quick settings and a page are one sheet of glass.
                    shell.ToggleQuick(dock,true,true);await Task.Delay(30);
                    Require(PanelMorph.Running(shell.Quick),"quick settings arrive as the glass settling");
                    await Task.Delay(500);Require(!PanelMorph.Running(shell.Quick)&&!shell.Quick.BackdropClip.HasValue,"the arrival ends as the plain panel");
                    var sheet=Bounds(shell.Quick);
                    shell.ShowDetail("bluetooth");
                    Require(PanelMorph.Running(shell.Detail)&&!shell.Quick.IsVisible&&shell.Detail.Sheet!=null&&shell.Detail.Surface.Opacity>=.99,"the tile grows into the page visibly from frame 0");
                    await Frames("page",30,70,120,180,260);await Task.Delay(300);Shot("page-open",shell.Detail);
                    var page=Bounds(shell.Detail);Require(!PanelMorph.Running(shell.Detail)&&!shell.Detail.BackdropClip.HasValue&&Math.Abs(page.Left-sheet.Left)<3&&Math.Abs(page.Top-sheet.Top)<3,"the page ends in place");
                    shell.ReturnToQuick(shell.Detail);
                    Require(shell.Quick.IsVisible&&PanelMorph.Running(shell.Quick)&&!shell.Detail.IsVisible&&shell.Quick.Surface.Opacity>=.99,"going back grows the same glass visibly from frame 0");
                    await Frames("back",30,70,120,180,260);await Task.Delay(300);
                    var again=Bounds(shell.Quick);Require(!PanelMorph.Running(shell.Quick)&&again.Bottom==sheet.Bottom&&again.Right==sheet.Right,"quick settings return to their place");
                    shell.Quick.HideImmediately();report.Add("pages ok");
                    // The media card opens into the output list and comes back.
                    var mediaBar=shell.DexMediaBar;mediaBar.ToggleMediaCard();await Task.Delay(520);Require(mediaBar.CardOpen,"the media card opens");Shot("card");
                    mediaBar.OutputFromCardForFixture();Require(!mediaBar.CardOpen&&shell.Detail.IsVisible&&PanelMorph.Running(shell.Detail)&&shell.Detail.OutputCard==mediaBar,"the card turns into the output list");
                    await Frames("output",30,90,170,270);await Task.Delay(350);Require(!PanelMorph.Running(shell.Detail)&&!shell.Detail.BackdropClip.HasValue,"the output list settles");Shot("output-open",shell.Detail);
                    var list=shell.Detail;Require(mediaBar.ReturnFromOutput(list)&&mediaBar.CardOpen&&PanelMorph.Running(list),"going back closes the glass around the card");
                    await Frames("output-back",30,90,170,250);await Task.Delay(300);Require(!list.IsVisible&&mediaBar.CardOpen,"the card is left in place");
                    mediaBar.ToggleMediaCard();await Task.Delay(350);Require(!mediaBar.CardOpen,"the card folds away");report.Add("output ok");
                    // The window keys: top status bar shows round keys for a maximized window without covering native title-bar buttons.
                    var sample=new Window {Title="Dex keys",Width=620,Height=380,Left=120,Top=140,ShowInTaskbar=false,Background=Brushes.WhiteSmoke,Topmost=true};extras.Add(sample);sample.Show();await Task.Delay(300);
                    var sampleHandle=new WindowInteropHelper(sample).Handle;
                    shell.DexKeysForFixture();shell.DexKeys.Pinned=true;shell.DexKeys.Follow(sampleHandle);await Task.Delay(300);
                    Require(shell.DexKeys.Target==sampleHandle&&!shell.DexKeys.CoverVisible&&shell.DexKeys.CoversShown==0,"native title-bar buttons stay uncovered");
                    Shot("keys");
                    sample.WindowState=WindowState.Maximized;await Task.Delay(500);shell.DexKeys.Update();await Task.Delay(300);
                    Require(shell.DexWindowKeysAtTop&&!shell.DexKeys.CoverVisible,"a maximized window has its keys by the battery without covering the app's own keys");
                    Shot("keys-maximized");
                    shell.DexKeys.Send(DexWindowKeys.Maximize);await Task.Delay(500);shell.DexKeys.Update();
                    Require(sample.WindowState==WindowState.Normal&&!shell.DexWindowKeysAtTop,"the key restores the window");
                    shell.DexKeys.Send(DexWindowKeys.Minimize);await Task.Delay(400);shell.DexKeys.Update();
                    Require(sample.WindowState==WindowState.Minimized&&!shell.DexKeys.CoverVisible,"the key minimizes the window");
                    shell.DexKeys.Pinned=false;sample.WindowState=WindowState.Normal;await Task.Delay(300);
                    DexWindowKeys.PlaceWindow(sampleHandle,new Rect(0,0,.5,1),screen);await Task.Delay(200);
                    var left=Bounds(sample);var area=Native.WorkArea(screen);Require(Math.Abs((left.Right+left.Left)/2-(area.Left+(area.Right-area.Left)/4))<24,"a layout puts the window in its half");
                    report.Add("window keys ok");
                    AppLog.Json("dex-probe.json",new {passed=true,steps=report.ToArray()});
                    result=0;
                }
                catch(Exception error){AppLog.Json("dex-probe.json",new {passed=false,error=error.ToString()});}
                finally
                {
                    Preferences.Set(p=>{p.DexDesktop=false;p.TopStatusBar=false;p.DockEdge=3;});
                    foreach(var window in extras)window.Close();
                    if(shell!=null)shell.DexKeysForFixtureClose();if(dock!=null)dock.Close();if(shell!=null)shell.Dispose();if(palette!=null)palette.Dispose();if(motion!=null)motion.Dispose();app.Shutdown(result);
                }
            }),System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            return app.Run();
        }
    }
    internal sealed partial class ShellController
    {
        bool fixtureTopBar;
        internal DockHost DexFixture(System.Windows.Forms.Screen screen)
        {
            var dock=new DockHost(this,screen);docks.Add(dock);dock.Show();dock.Refresh(Power);
            fixtureTopBar=true;
            topLeft=new TopStatusBar(this,TopStatusBar.Side.Left);topRight=new TopStatusBar(this,TopStatusBar.Side.Right);
            topLeft.Present(screen,dock.Scale,false);topRight.Present(screen,dock.Scale,false);
            return dock;
        }
        internal GlassWindow DexClockBar {get{return topLeft;}}
        internal TopStatusBar DexMediaBar {get{return topRight;}}
        internal bool DexWindowKeysAtTop {get{return topRight!=null&&topRight.WindowKeysShown;}}
        internal void DexKeysForFixture(){EnsureDexKeys();}
        internal void DexKeysForFixtureClose(){CloseDexKeys();}
    }
}
