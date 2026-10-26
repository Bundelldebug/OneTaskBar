using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class IssueFollowupChecks
    {
        static void Require(bool value,string text){if(!value)throw new InvalidOperationException("Issue follow-up: "+text);}
        static Native.RectI Bounds(int left,int top,int right,int bottom){return new Native.RectI {Left=left,Top=top,Right=right,Bottom=bottom};}
        internal static void Validate(List<string> checks)
        {
            var left=new System.Drawing.Rectangle(-1920,-200,1920,1080);var right=new System.Drawing.Rectangle(0,0,2560,1440);
            var full=new FullscreenCandidate {Monitor=left,Bounds=Bounds(-1920,-200,0,880),Client=Bounds(-1920,-200,0,880)};
            var normal=new FullscreenCandidate {Monitor=right,Bounds=Bounds(50,50,1400,1000),Client=Bounds(55,80,1395,995),Framed=true};var windows=new[]{normal,full};
            Require(FullscreenVisibility.IsFullScreen(left,windows)&&!FullscreenVisibility.IsFullScreen(right,windows),"inactive fullscreen on a second monitor hides only that monitor's dock");
            var covered=new FullscreenCandidate {Monitor=left,Bounds=Bounds(-1800,-100,-400,700),Client=Bounds(-1795,-70,-405,695),Framed=true};Require(!FullscreenVisibility.IsFullScreen(left,new[]{covered,full}),"a normal window above fullscreen restores that monitor's dock");
            normal.Bounds=Bounds(0,0,2560,1440);normal.Client=Bounds(0,32,2560,1440);normal.Maximized=true;Require(!FullscreenVisibility.IsFullScreen(right,new[]{normal}),"ordinary maximized framed windows do not hide the dock");normal.Client=normal.Bounds;Require(FullscreenVisibility.IsFullScreen(right,new[]{normal}),"fullscreen clients are recognized even when providers retain maximized frame flags");
            full.Bounds=full.Client=Bounds(-1920,-200,2560,1440);Require(FullscreenVisibility.IsFullScreen(left,new[]{full})&&FullscreenVisibility.IsFullScreen(right,new[]{full}),"a spanning fullscreen client hides both docks");
            checks.Add("Fullscreen is classified per monitor in stacking order, including inactive secondary displays, normal occluders, retained fullscreen frame flags and negative-origin spanning layouts");
        }
        internal static async Task Fallback(ShellController shell,System.Windows.Forms.Screen screen)
        {
            var source=new Window {Title="One TaskBar fullscreen fixture",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Width=120,Height=90,ShowInTaskbar=false,ShowActivated=false};try{source.Show();var handle=new System.Windows.Interop.WindowInteropHelper(source).Handle;Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80);Native.Place(source,screen.Bounds,20,20,120,90);Require(FullscreenVisibility.Inspect(handle)==null,"ordinary tool overlays do not conceal fullscreen state");double scale=Native.PrepareMonitor(source,screen.Bounds);Native.Place(source,screen.Bounds,0,0,screen.Bounds.Width/scale,screen.Bounds.Height/scale);await Task.Delay(30);var candidate=FullscreenVisibility.Inspect(handle);Require(candidate!=null&&FullscreenVisibility.IsFullScreen(screen.Bounds,new[]{candidate}),"actual fullscreen client geometry is recognized even for tool-window media providers");}finally{source.Close();}
            int oldUI=Preferences.Current.UIVersion,count=LiveBackdrop.InstanceCount;try{using(LiveBackdrop.WithoutHostForFixture())foreach(int ui in ReleaseFeatures.GlassUI?new[]{85,90,95}:new[]{85,90})foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                Preferences.Current.UIVersion=ui;var start=new StartWindow(shell);try{start.ShowAt(screen.Bounds,20,20,940,620);await Task.Delay(30);var color=start.Surface.Background as SolidColorBrush;Require(!start.HasBlur&&!Native.RealtimeBackdrop(start)&&color!=null&&color.Color.A==255,"unsupported backdrops use a real opaque fallback");Require(dark?color.Color.R<80:color.Color.R>200,"fallback background follows the text palette instead of turning a light panel black");if(ui==90&&!dark)Program.Save(start.Surface,940,620,Path.Combine(AppLog.Data,"start-unsupported-blur-light.png"));}finally{start.Close();}
            }}finally{Preferences.Current.UIVersion=oldUI;}Require(LiveBackdrop.InstanceCount==count,"failed backdrop creation leaves no live host");
        }
    }
}
