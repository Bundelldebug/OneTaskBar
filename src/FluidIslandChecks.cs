using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // The Fluid UI (10.0 beta): its version switch, the elastic spring and the island's fluid material.
    internal static class FluidIslandChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Fluid island: "+message);}

        static BitmapSource Cover()
        {
            var visual=new DrawingVisual();
            using(var context=visual.RenderOpen())
            {
                context.DrawRectangle(Brushes.Coral,null,new Rect(0,0,192,216));
                context.DrawRectangle(Brushes.Turquoise,null,new Rect(192,0,192,216));
                context.DrawEllipse(Brushes.Gold,null,new Point(260,90),58,58);
            }
            var image=new RenderTargetBitmap(384,216,96,96,PixelFormats.Pbgra32);
            image.Render(visual);
            image.Freeze();
            return image;
        }

        // Pure checks that need no window.
        internal static void Validate()
        {
            Require(new PreferenceData().UIVersion==85,"existing installations keep their UI version on upgrade");
            foreach(double step in new[]{1/60.0,1/144.0})
            {
                var spring=new SpringValue(.002) {Omega=30,Damping=.6};
                spring.Target=1;
                double peak=0;
                for(int i=0;i<(int)(1.5/step);i++){spring.Step(step);peak=Math.Max(peak,spring.Value);}
                Require(peak>1.05&&peak<1.15,"the elastic spring overshoots by about a tenth: "+peak);
                Require(spring.AtRest&&Math.Abs(spring.Value-1)<.002,"the elastic spring settles on its target");
            }
            var slow=new SpringValue(.002) {Omega=30,Damping=.6};var quick=new SpringValue(.002) {Omega=30,Damping=.6};
            slow.Target=quick.Target=1;
            for(int i=0;i<30;i++)slow.Step(1/60.0);
            for(int i=0;i<72;i++)quick.Step(1/144.0);
            Require(Math.Abs(slow.Value-quick.Value)<.001,"the elastic spring is the same at 60 and 144 frames a second");
            var critical=new SpringValue(.002) {Omega=30};critical.Target=1;
            double max=0;
            for(int i=0;i<200;i++){critical.Step(1/120.0);max=Math.Max(max,critical.Value);}
            Require(max<=1.0001,"the default spring still never overshoots");
            int old=Preferences.Current.UIVersion;
            try
            {
                Preferences.Set(p=>p.UIVersion=95);
                Require(!Fluid.Enabled,"9.5 is not the fluid UI");
                var plainTint=LiquidGlass.Tint(true,0);var plainRim=Glass95.Edge(true,true);
                Preferences.Set(p=>p.UIVersion=Fluid.Version);
                Require(Fluid.Enabled,"UI version 10.0 is the fluid UI");
                Require(Glass95.Enabled&&Preferences.Current.UIVersion>=95,"the fluid UI keeps every 9.5 glass behavior");
                Require(!Object.ReferenceEquals(LiquidGlass.Tint(true,0),plainTint)&&LiquidGlass.Tint(true,0).IsFrozen,"the fluid UI has its own frozen thin tint");
                Require(Object.ReferenceEquals(LiquidGlass.Tint(true,2),LiquidGlass.Tint(true,2)),"readability steps keep their cached tints");
                Require(!Object.ReferenceEquals(Glass95.Rim,plainRim),"the fluid UI has its own warm rim");
                var choice=Array.Find(UIVersions.Available,c=>c.Value==Fluid.Version);
                Require(choice!=null&&choice.Label.IndexOf("베타")>=0,"the picker offers the fluid UI with a beta tag");
            }
            finally{Preferences.Set(p=>p.UIVersion=old);}
        }
        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;DynamicIsland view=null;IDisposable motion=null;
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    Validate();
                    Preferences.Set(p=>p.UIVersion=Fluid.Version);
                    motion=Motion.WithAnimationsForFixture();
                    var panel=new GlassWindow {Width=320,Height=220,Left=1200,Top=200,ShowActivated=false};
                    panel.Surface.Child=new TextBlock {Text="Fluid panel",Foreground=Brushes.Black,Margin=new Thickness(24),FontSize=18};
                    panel.Show();
                    await Task.Delay(120);
                    Program.Save(panel.Content as FrameworkElement,320,220,Path.Combine(AppLog.Data,"fluid-panel-sweep.png"));
                    await Task.Delay(900);
                    Program.Save(panel.Content as FrameworkElement,320,220,Path.Combine(AppLog.Data,"fluid-panel-rest.png"));
                    panel.Hide();panel.Show();
                    await Task.Delay(140);
                    Program.Save(panel.Content as FrameworkElement,320,220,Path.Combine(AppLog.Data,"fluid-panel-sweep.png"));
                    Require(panel.HasFluidLight,"the fluid UI adds its light to glass panels");
                    panel.Close();
                    foreach(int version in new[]{95,Fluid.Version})Program.Save(new UIVersionPreview {Version=version},130,132,Path.Combine(AppLog.Data,"ui-version-preview-"+version+".png"));
                    view=new DynamicIsland(command=>{},value=>Task.FromResult(true),()=>{},id=>{});
                    var screen=Forms.Screen.PrimaryScreen;
                    double dpi=Native.PrepareMonitor(view,screen.Bounds);
                    var media=new MediaState {Available=true,Active=true,Playing=true,Title="Ordinary · 오디너리",Artist="Damage",CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Duration=200,Position=45,Artwork=Cover()};
                    view.Present(media,"헤드폰",screen,dpi);
                    await Task.Delay(500);
                    Require(view.FluidActive&&view.Surface.Background!=Brushes.Black&&view.Surface.Background.IsFrozen,"the fluid island uses its own frozen material");
                    Require(view.FluidMoving,"light and orb move while music plays");
                    Require(!view.HasBlur,"the fluid island still creates no live backdrop");
                    Program.Save(view.Surface,(int)DynamicIslandLayout.MediaWidth,(int)DynamicIslandLayout.CompactHeight,Path.Combine(AppLog.Data,"fluid-island-media.png"));
                    var tall=new double[1];double peak=0;
                    view.SetExpanded(true);
                    for(int i=0;i<60;i++){await Task.Delay(8);peak=Math.Max(peak,view.Shape.Height);}
                    await Task.Delay(400);
                    double target=view.Shape.Height;
                    Require(!view.Animating&&target>100,"the fluid card settles");
                    Require(peak>target+1,"the card stretches past its size and settles back: peak "+peak+" target "+target);
                    Require(peak<=DynamicIslandLayout.MaximumHeight+.01,"the stretch never leaves the host window");
                    Program.Save(view.Surface,360,204,Path.Combine(AppLog.Data,"fluid-island-expanded.png"));
                    view.SetExpanded(false);
                    await Task.Delay(700);
                    Require(!view.Animating&&view.Shape.Height==DynamicIslandLayout.CompactHeight&&view.Shape.Width==DynamicIslandLayout.MediaWidth,"collapse settles exactly on the compact capsule");
                    var notice=new NotificationEntry {Id=1,App="테스트 앱",AppId="fixture",Title="Emily Olson",Body="Yup. It was Dan. Bakery I wanted to…",Icon=media.Artwork};
                    Require(view.ShowNotification(notice,entry=>{}),"notifications still use the island");
                    // The arrival, frame by frame: light bursts around the orb, pours across, words come into focus.
                    int passed=0;
                    foreach(int at in new[]{70,170,290,430,640,1000})
                    {
                        await Task.Delay(at-passed);passed=at;
                        Program.Save(view.Surface,360,156,Path.Combine(AppLog.Data,"fluid-island-arrival-"+at+".png"));
                    }
                    Program.Save(view.Surface,360,156,Path.Combine(AppLog.Data,"fluid-island-notification.png"));
                    view.DismissNotification();
                    await Task.Delay(500);
                    media.Playing=false;
                    view.Present(media,"헤드폰",screen,dpi);
                    await Task.Delay(100);
                    Require(!view.FluidMoving,"pausing stops the drifting light and the orb");
                    Preferences.Set(p=>p.UIVersion=95);
                    view.Present(media,"헤드폰",screen,dpi);
                    await Task.Delay(500);
                    Require(!view.FluidActive&&view.Surface.Background==Brushes.Black&&!view.FluidMoving,"turning fluid off returns the black island");
                    view.Suspend();
                    AppLog.Json("fluid-island-probe.json",new {passed=true,peak=peak,target=target,versionSwitch=true,material=true,pausedIsStill=true});
                    result=0;
                }
                catch(Exception error){AppLog.Json("fluid-island-probe.json",new {passed=false,error=error.ToString(),island=view==null?null:view.Inspect()});}
                finally{if(view!=null)view.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            app.Run();return result;
        }
    }
}
