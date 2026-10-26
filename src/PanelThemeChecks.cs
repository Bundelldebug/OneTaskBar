using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class PanelThemeChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Panel themes: "+message);}
        static double Channel(byte value){double n=value/255.0;return n<=.04045?n/12.92:Math.Pow((n+.055)/1.055,2.4);}
        static double Light(Color color){return .2126*Channel(color.R)+.7152*Channel(color.G)+.0722*Channel(color.B);}
        static double Contrast(Color a,Color b){double x=Light(a),y=Light(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        static Color Blend(Color front,Color back,double opacity=1){double a=front.A/255.0*opacity;return Color.FromRgb((byte)Math.Round(front.R*a+back.R*(1-a)),(byte)Math.Round(front.G*a+back.G*(1-a)),(byte)Math.Round(front.B*a+back.B*(1-a)));}
        static IEnumerable<Color> ColorsOf(Brush brush){var solid=brush as SolidColorBrush;if(solid!=null)yield return solid.Color;var gradient=brush as LinearGradientBrush;if(gradient!=null)foreach(var stop in gradient.GradientStops)yield return stop.Color;}
        static IEnumerable<Color> Backdrops(){foreach(byte r in new byte[]{0,64,128,192,255})foreach(byte g in new byte[]{0,64,128,192,255})foreach(byte b in new byte[]{0,64,128,192,255})yield return Color.FromRgb(r,g,b);}
        internal static void Validate(List<string> checks)
        {
            if(!SystemParameters.HighContrast)foreach(bool dark in new[]{false,true})foreach(bool modern in new[]{false,true}){var surface=PanelMaterial.PanelSurface(dark,modern);Require(surface.IsFrozen&&Object.ReferenceEquals(surface,PanelMaterial.PanelSurface(dark,modern)),"surfaces reuse frozen palette resources");if(!dark){Require(Object.ReferenceEquals(surface,PanelMaterial.Surface(true,modern)),"light mode reuses the previous dark material without changing its tint or transparency");continue;}foreach(var tint in ColorsOf(surface))foreach(var background in Backdrops()){var painted=Blend(tint,background);Require(Contrast(((SolidColorBrush)PanelMaterial.Text(dark)).Color,painted)>=7,"new dark body text has strong contrast across declared backdrops");Require(Contrast(((SolidColorBrush)PanelMaterial.Muted(dark)).Color,painted)>=4.5,"new dark secondary text remains readable across declared backdrops");}}
            Require(PanelMaterial.DarkPanelOpacity>PanelMaterial.LightPanelOpacity&&PanelMaterial.Transparency==70,"new dark panels are darker while classic light and dock transparency stay fixed");checks.Add("Light panels retain the former dark material; new dark panels keep primary/secondary contrast across 125 declared backgrounds with frozen palette resources");
        }
        static object Matrix(int ui,bool dark)
        {
            double primary=Double.MaxValue,secondary=Double.MaxValue,focus=Double.MaxValue;var text=((SolidColorBrush)Paint.Text).Color;var muted=((SolidColorBrush)Paint.Muted).Color;
            Brush testedSurface=dark?PanelMaterial.PanelSurface(true,ui>=95):PanelMaterial.Solid(false);
            if(!dark){Require(((SolidColorBrush)Paint.Text).Color==(Color)ColorConverter.ConvertFromString(ui>=95?"#FFF5F7FC":"#FFFFFFFF"),"classic light mode keeps the old dark primary text");Require(((SolidColorBrush)Paint.Muted).Color==(Color)ColorConverter.ConvertFromString(ui>=95?"#FFD1D9E7":"#FFDBE3DF"),"classic light mode keeps the old dark secondary text");Require(QuickPanel.Card== (ui>=95?Glass95.Card:PanelMaterial.DarkCard),"classic light cards retain the old dark material");}
            foreach(var tint in ColorsOf(testedSurface))foreach(var background in Backdrops()){
                var surface=Blend(tint,background);foreach(var card in ColorsOf(QuickPanel.Card))foreach(byte hover in new byte[]{0,28}){var painted=Blend(Color.FromArgb(hover,255,255,255),Blend(card,surface));primary=Math.Min(primary,Contrast(text,painted));secondary=Math.Min(secondary,Contrast(muted,painted));}
                var field=Blend(((SolidColorBrush)PanelMaterial.Field).Color,surface);primary=Math.Min(primary,Contrast(text,field));secondary=Math.Min(secondary,Contrast(muted,field));focus=Math.Min(focus,Contrast(((SolidColorBrush)PanelMaterial.Focus).Color,field));
            }
            Require(primary>=4.5&&secondary>=4.5&&focus>=3,"text, placeholders and keyboard focus meet the declared-color contrast bounds: "+primary+", "+secondary+", "+focus);return new {ui=ui,dark=dark,primary=primary,secondary=secondary,focus=focus,backdrops=125,contrastScope=dark?"declared backdrops":"opaque fallback; classic live material unchanged"};
        }
        static void Surface(GlassWindow window,bool dark)
        {
            Require(window.Dark==dark,"window material follows its selected panel mode");Brush expected=window.HasBlur&&Preferences.Current.Blur?PanelMaterial.PanelSurface(dark,Glass95.Enabled):PanelMaterial.Solid(dark);Require(window.Surface.Background==expected,"the real displayed surface uses the shared panel palette");if(window.HasBlur)Require(Native.RealtimeBackdrop(window),"live blur remains available under the new tint");
        }
        static void ClosePanels(ShellController shell){if(shell.Quick!=null)shell.Quick.Close();if(shell.Center!=null)shell.Center.Close();if(shell.StartMenu!=null)shell.StartMenu.Close();if(shell.Drawer!=null)shell.Drawer.Close();if(shell.Finder!=null)shell.Finder.Close();shell.Quick=null;shell.Center=null;shell.StartMenu=null;shell.Drawer=null;shell.Finder=null;}
        static void OpenPanels(ShellController shell,System.Windows.Forms.Screen screen)
        {
            shell.Quick=new QuickWindow(shell);shell.Quick.Refresh(new PowerState {Known=true,Level=76},64,35,false,true,true);shell.Center=new NotificationWindow(shell);shell.Center.Refresh();shell.StartMenu=new StartWindow(shell);shell.StartMenu.Configure(940,720);shell.StartMenu.RefreshPins();shell.StartMenu.FixtureRecent(new List<StartRecent>{new StartRecent {App=shell.Apps[0],Time=DateTime.Now}});shell.Drawer=new DrawerWindow(shell);shell.Drawer.Configure(720,680);shell.Drawer.Reload();shell.Finder=new SearchWindow(shell);shell.Finder.Configure(720,680);shell.Finder.Reload();foreach(var window in Panels(shell))window.ShowAt(screen.Bounds,20,20,window.Width,window.Height);
        }
        static GlassWindow[] Panels(ShellController shell){return new GlassWindow[]{shell.Quick,shell.Center,shell.StartMenu,shell.Drawer,shell.Finder};}
        static void Feedback(ShellController shell,System.Windows.Forms.Screen screen,int ui,bool dark)
        {
            var state=SamsungPerformance.Parse("[\"0\",\"1\",\"2\",\"3\"]",3);var picker=new SamsungModePicker(state.Modes,_=>{});
            foreach(var access in new[]{SamsungDirectAccess.Ready,SamsungDirectAccess.Denied})foreach(int selected in new[]{0,1,2,3}){state.Selected=selected;picker.Refresh(state,false,access);LightPanel.Apply(picker);Glass95.RefreshColors(picker);foreach(var button in picker.Buttons)Require(((Glyph)button.Content).Color==((int)button.Tag==selected?Paint.OnLight:Paint.Text),"every selected Samsung mode retains dark ink in its white circle, even when direct control is unavailable");}
            state.Selected=3;picker.Refresh(state,false,SamsungDirectAccess.Denied);if(ui==(ReleaseFeatures.GlassUI?95:90))Program.Save(picker,340,115,Path.Combine(AppLog.Data,"samsung-mode-"+(dark?"dark":"light")+".png"));
            foreach(var tint in ColorsOf(PanelMaterial.PopupSurface(dark)))foreach(var background in Backdrops()){var painted=Blend(tint,background);Require(Object.ReferenceEquals(PanelMaterial.PopupSurface(dark),QuickPanel.Glass),"floating windows use exactly the requested quick-settings tint");if(dark)Require(Contrast(((SolidColorBrush)Paint.Text).Color,painted)>=7&&Contrast(((SolidColorBrush)Paint.Muted).Color,painted)>=4.5,"new dark floating text keeps its declared contrast");}
            var entry=new NotificationEntry {Id=2,AppId="fixture.banner",App="테마 확인",Title="새로운 알림이 있습니다",Body="밝은 화면 위에서도 알림 내용을 확인합니다.",Created=DateTimeOffset.Now};
            var banner=new NotificationBanner(entry,_=>{},(w,remove)=>{});var buds=new BudsConnectionPopup(()=>{},()=>{},_=>{});buds.Update(new BudsInfo {Name="테스트 Buds4 Pro",Detail="왼쪽 · 오른쪽 89% | 케이스 80%"});buds.Opacity=1;
            try{foreach(var popup in new GlassWindow[]{banner,buds}){popup.ShowAt(screen.Bounds,30,30,360,126);Require(popup.Surface.Background==(popup.HasBlur?PanelMaterial.PopupSurface(dark):PanelMaterial.Solid(dark)),"notification and Buds use exactly the quick-settings material");if(ui==(ReleaseFeatures.GlassUI?95:90))Program.Save(popup.Surface,360,126,Path.Combine(AppLog.Data,(popup==banner?"banner":"buds")+"-"+(dark?"dark":"light")+".png"));}}finally{banner.Close();buds.Close();}
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ShellController shell=null;int result=1,oldUI=Preferences.Current.UIVersion,oldMode=Preferences.Current.UITheme;bool oldAnimations=Preferences.Current.Animations;var samples=new List<object>();
            app.Dispatcher.BeginInvoke(new Action(async()=>{try{
                Program.Inspectable=true;Directory.CreateDirectory(AppLog.Data);Preferences.Current.Animations=false;Preferences.Current.UITheme=1;shell=new ShellController(true);shell.Apps=Enumerable.Range(1,10).Select(i=>new AppEntry {Name="테마 확인 앱 "+i,AppID="fixture.theme."+i,Icon=ArtworkPalette.Sample(i%2==0?System.Windows.Media.Colors.CornflowerBlue:System.Windows.Media.Colors.Teal)}).ToList();shell.StartPins.Replace(shell.Apps);shell.Notifications.Items=new List<NotificationEntry>{new NotificationEntry {Id=1,AppId="fixture.theme",App="테마 확인",Title="새로운 알림이 있습니다",Body="라이트와 다크 모드에서 알림 내용을 확인할 수 있습니다.",Created=DateTimeOffset.Now}};var screen=System.Windows.Forms.Screen.PrimaryScreen;
                foreach(int ui in ReleaseFeatures.GlassUI?new[]{85,90,95}:new[]{85,90})foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                    Preferences.Current.UIVersion=ui;samples.Add(Matrix(ui,dark));OpenPanels(shell,screen);await Task.Delay(80);foreach(var window in Panels(shell)){Surface(window,dark);if(ui==(ReleaseFeatures.GlassUI?95:90)){string name=window is QuickWindow?"quick":window is NotificationWindow?"notifications":window is StartWindow?"start":window is DrawerWindow?"apps":"search";Program.Save(window.Surface,(int)window.Width,(int)window.Height,Path.Combine(AppLog.Data,"panel-"+name+(dark?"-dark":"-light")+".png"));}}Require(shell.StartMenu.Search.Foreground==Paint.Text&&shell.Drawer.Search.Foreground==Paint.Text&&shell.Finder.Query.Foreground==Paint.Text,"all launcher input text shares the same palette");ClosePanels(shell);Feedback(shell,screen,ui,dark);
                }
                Preferences.Current.UIVersion=ReleaseFeatures.GlassUI?95:90;OpenPanels(shell,screen);shell.StartMenu.ShowRecentPage(true);shell.Drawer.RestoreSearch("보존");shell.Finder.Query.Text="계";
                foreach(bool dark in new[]{true,false})using(Glass95.WithDarkForFixture(dark)){shell.RefreshPanelTheme();await Task.Delay(60);foreach(var window in Panels(shell))Surface(window,dark);Require(shell.StartMenu.RecentPage&&shell.Drawer.Search.Text=="보존"&&shell.Finder.Query.Text=="계","theme switching preserves recent More and search state without opening another search");}
                ClosePanels(shell);Require(LiveBackdrop.OrphanCount==0,"theme switching leaves no orphan backdrop host");AppLog.Json("panel-theme-probe.json",new {passed=true,samples=samples,windows=5,uiVersions=ReleaseFeatures.GlassUI?3:2,palettes=2,themeStatePreserved=true,osThemeChanged=false,desktopPixelsRead=false,deviceValuesChanged=false});result=0;
            }catch(Exception error){AppLog.Json("panel-theme-probe.json",new {passed=false,error=error.ToString(),samples=samples});}finally{if(shell!=null){ClosePanels(shell);shell.Dispose();}Preferences.Current.UIVersion=oldUI;Preferences.Current.UITheme=oldMode;Preferences.Current.Animations=oldAnimations;app.Shutdown(result);}}));app.Run();return result;
        }
    }
}
