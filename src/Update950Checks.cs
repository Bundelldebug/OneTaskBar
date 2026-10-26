using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace OneTaskBar
{
    internal static class Update950Checks
    {
        static void Require(bool value,string text){if(!value)throw new InvalidOperationException(text);}
        internal static void Validate(List<string> checks)
        {
            ReleaseUIFeedbackChecks.Validate(checks);
            CompactStatusChecks.Validate(checks);
            IssueFollowupChecks.Validate(checks);
            PanelThemeChecks.Validate(checks);
            foreach(bool windowsDark in new[]{false,true}){Require(AppTheme.Resolve(0,windowsDark)==windowsDark,"Follow Windows palette");Require(!AppTheme.Resolve(1,windowsDark)&&AppTheme.Resolve(2,windowsDark),"Explicit app palettes are independent");}
            foreach(uint state in new uint[]{0,1,2,3})Require(TaskbarSpace.Physical(state,false)==(state|1U)&&TaskbarSpace.Physical(state,true)==(state&~1U),"Work-area lease preserves unrelated taskbar flags");
            Require(RecentThumbnails.Supported(@"C:\Pictures\photo.png")&&RecentThumbnails.Supported(@"D:\Films\movie.mp4")&&!RecentThumbnails.Supported(@"\\server\private\photo.jpg")&&!RecentThumbnails.Supported(@"C:\Programs\app.exe")&&!RecentThumbnails.Supported("https://example.com/photo.png"),"Recent preview paths and types are limited");
            var normalized=Preferences.Normalize(new PreferenceData {UIVersion=95,UITheme=2});Require(normalized.UIVersion==(ReleaseFeatures.GlassUI?95:90)&&normalized.UITheme==2,"Stable build excludes UI 9.5 without discarding theme settings");
            var monitor=new System.Drawing.Rectangle(-1920,0,1920,1080);var full=new Native.RectI {Left=-1920,Top=0,Right=0,Bottom=1080};Require(BrowserFullscreen.Eligible(full,monitor,0x40000)&&!BrowserFullscreen.Eligible(full,monitor,0xC40000),"Fullscreen correction excludes captioned maximized windows");full.Bottom-=68;Require(!BrowserFullscreen.Eligible(full,monitor,0),"Work-area windows are not fullscreen");Require(BrowserFullscreen.SamsungPath(@"C:\Program Files\Samsung\Internet\Application\samsunginternet.exe")&&!BrowserFullscreen.SamsungPath(@"C:\Other\samsunginternet.exe"),"Fullscreen fix is limited to Samsung Browser");
            checks.Add("App-only theme, bounded local previews, release UI availability and recoverable taskbar space flags pass");
        }
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {var own=root as T;if(own!=null)yield return own;foreach(object value in LogicalTreeHelper.GetChildren(root)){var child=value as DependencyObject;if(child!=null)foreach(var result in Children<T>(child))yield return result;}}
        static void FullscreenFrame()
        {
            var window=new Window {WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Width=160,Height=90,ShowInTaskbar=false,ShowActivated=false};var fix=new BrowserFullscreen();try{window.Show();var h=new System.Windows.Interop.WindowInteropHelper(window).Handle;int original=Native.GetWindowLong(h,-16)|0x40000;Native.SetWindowLong(h,-16,original);int border,corner;bool canRead=Native.DwmGetWindowAttribute(h,34,out border,4)==0;Native.DwmGetWindowAttribute(h,33,out corner,4);fix.ApplyForFixture(h);int next;Require((Native.GetWindowLong(h,-16)&0x40000)==0,"Fullscreen resize border is suppressed");fix.Restore();Require(Native.GetWindowLong(h,-16)==original&&(!canRead||Native.DwmGetWindowAttribute(h,34,out next,4)==0&&next==border),"Original native frame is restored");}finally{fix.Dispose();window.Close();}
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1,oldUI=Preferences.Current.UIVersion,oldTheme=Preferences.Current.UITheme;bool osDark=WindowsTheme.Read().Dark;ShellController shell=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;Directory.CreateDirectory(AppLog.Data);Validate(new List<string>());FullscreenFrame();Preferences.Current.UIVersion=ReleaseFeatures.GlassUI?95:90;Preferences.Current.UITheme=1;shell=new ShellController(true);var screen=System.Windows.Forms.Screen.PrimaryScreen;
                string photo=Path.Combine(AppLog.Data,"recent-preview.png");var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(Brushes.Coral,null,new Rect(0,0,512,288));dc.DrawEllipse(Brushes.SteelBlue,null,new Point(256,144),80,80);}var bitmap=new RenderTargetBitmap(512,288,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(photo))encoder.Save(file);
                var loaded=await RecentThumbnails.Get(photo,CancellationToken.None);Require(loaded!=null&&loaded.IsFrozen&&loaded.PixelWidth==128&&loaded.PixelHeight==72,"Preview is bounded, frozen and keeps its aspect ratio");Require(Object.ReferenceEquals(loaded,await RecentThumbnails.Get(photo,CancellationToken.None)),"Unchanged preview reuses the cache");
                shell.StartMenu=new StartWindow(shell);shell.StartMenu.FixtureRecent(new List<StartRecent>{new StartRecent {App=new AppEntry {Name="Photo preview",Target=photo,Executable=photo},Time=DateTime.Now}});shell.StartMenu.ShowAt(screen.Bounds,20,20,940,700);
                for(int i=0;i<80&&!Children<RecentThumbnail>(shell.StartMenu).Any(t=>t.HasPreview);i++)await Task.Delay(25);Require(Children<RecentThumbnail>(shell.StartMenu).Any(t=>t.HasPreview),"Start displays the actual photo preview");Program.Save(shell.StartMenu.Surface,940,700,Path.Combine(AppLog.Data,"start-recent-preview.png"));
                shell.Quick=new QuickWindow(shell);shell.Quick.ShowAt(screen.Bounds,30,30,380,650);shell.Welcome=new WelcomeWindow(shell);shell.Welcome.ShowAt(screen.Bounds,430,30,760,720);Require(!Glass95.Dark&&!AppSettingsPalette.Dark,"App light mode applies to settings and panels");
                var old=shell.Quick;Preferences.Set(p=>p.UITheme=2);await Task.Delay(200);Require(Glass95.Dark&&AppSettingsPalette.Dark&&shell.Quick!=null&&!Object.ReferenceEquals(old,shell.Quick)&&shell.Quick.IsVisible,"App dark mode rebuilds visible panels");Require(WindowsTheme.Read().Dark==osDark,"App palette never changes Windows");
                Preferences.Set(p=>p.UITheme=1);await Task.Delay(200);Require(!Glass95.Dark&&!AppSettingsPalette.Dark&&shell.Welcome.IsVisible,"Returning to light restores the app palette");int versions=shell.Welcome.VersionPicker.Items.Count;Require(versions==(ReleaseFeatures.GlassUI?3:2),"UI selector exposes only supported versions");
                using(var file=new FileStream(photo,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Require(file.Length>0,"Decoder releases the file");Require(RecentThumbnails.CacheCount<=48,"Preview cache is bounded");
                await ReleaseUIFeedbackChecks.Run(shell);
                AppLog.Json("update950-probe.json",new {passed=true,appThemeIndependent=true,windowsThemeUnchanged=true,recentPhotoPreview=true,bounded128px=true,fileUnlocked=true,stableExcludesGlassUI=!ReleaseFeatures.GlassUI,uiChoices=versions,connectedRadioContrast=true,uniformActionCircles=true,scalableVersionList=true,keyboardStartOutsideDismiss=true,nonactivatedStartOutsideDismiss=true,contextMenuPreserved=true});result=0;
            }catch(Exception error){AppLog.Json("update950-probe.json",new {passed=false,error=error.ToString()});}finally{if(shell!=null)shell.Dispose();Preferences.Current.UIVersion=oldUI;Preferences.Current.UITheme=oldTheme;app.Shutdown(result);}}),DispatcherPriority.ApplicationIdle);return app.Run();
        }
    }
}
