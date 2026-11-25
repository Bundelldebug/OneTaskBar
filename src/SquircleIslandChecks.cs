using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // The squircle island option: its outline, the folded pill with tile and title, the rim colour, the card it grows
    // into, the small island that splits off while charging over music, and the return to the classic island.
    internal static class SquircleIslandChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Squircle island: "+message);}

        static BitmapSource Cover()
        {
            var visual=new DrawingVisual();
            using(var context=visual.RenderOpen())
            {
                context.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xB0,0x18,0x20)),null,new Rect(0,0,200,200));
                context.DrawEllipse(Brushes.Gold,null,new Point(120,90),40,40);
            }
            var image=new RenderTargetBitmap(200,200,96,96,PixelFormats.Pbgra32);
            image.Render(visual);
            image.Freeze();
            return image;
        }

        // Pure checks that need no window.
        internal static void Validate()
        {
            Require(!new PreferenceData().SquircleIsland,"the option is off by default");
            var box=new Rect(0,0,164,22);
            var pill=IslandSquircleShape.Create(box,11);
            var round=new RectangleGeometry(box,11,11);
            double area=pill.GetArea(),roundArea=round.GetArea(),full=box.Width*box.Height;
            Require(area>roundArea&&area<full,"the squircle pill is fuller than a capsule and smaller than its box: "+area+" vs "+roundArea);
            var bounds=pill.Bounds;
            Require(Math.Abs(bounds.Left)<.01&&Math.Abs(bounds.Top)<.01&&Math.Abs(bounds.Right-164)<.01&&Math.Abs(bounds.Bottom-22)<.01,"the outline fills its box exactly: "+bounds);
            Require(pill.FillContains(new Point(82,11))&&!pill.FillContains(new Point(.4,.4)),"the outline is closed and cuts the corners");
            var card=IslandSquircleShape.Create(new Rect(0,0,360,204),34);
            Require(card.FillContains(new Point(180,102))&&!card.FillContains(new Point(1,1))&&card.FillContains(new Point(180,1)),"the card has straight sides and cut corners");
            Require(Math.Abs(IslandSquircleShape.Create(new Rect(0,0,10,10),99).Bounds.Width-10)<.01,"a radius larger than the shape is limited");
            Require(pill.IsFrozen,"the outline is frozen");
            Require(DynamicIslandLayout.MediaPillWidth(true)>DynamicIslandLayout.MediaPillWidth(false),"the squircle pill is wider than the classic media capsule");
            var state=new DynamicIslandState {Squircle=true};
            Require(state.Size(true,DateTime.UtcNow).Height==DynamicIslandLayout.CompactHeight,"the squircle pill keeps the compact height, so the safe area is unchanged");
            Require(DynamicIslandLayout.CompactHeight+DynamicIslandLayout.TopInset<=IslandSafeArea.Height,"the folded pill fits the reserved band");
        }

        internal static int Run()
        {
            var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
            int result=1;DynamicIsland view=null;IDisposable motion=null;bool oldSquircle=Preferences.Current.SquircleIsland;
            app.Dispatcher.BeginInvoke(new Action(async()=>
            {
                try
                {
                    Program.Inspectable=true;
                    Validate();
                    motion=Motion.WithAnimationsForFixture();
                    Preferences.Set(p=>p.SquircleIsland=true);
                    view=new DynamicIsland(command=>{},value=>Task.FromResult(true),()=>{},id=>{});
                    var screen=Forms.Screen.PrimaryScreen;
                    double dpi=Native.PrepareMonitor(view,screen.Bounds);
                    var media=new MediaState {Available=true,Active=true,Playing=true,Title="Many Men (Wish Death)",Artist="50 Cent",CanPlay=true,CanPrevious=true,CanNext=true,CanSeek=true,Duration=256,Position=251,Artwork=Cover()};
                    view.Present(media,"Phone speaker",screen,dpi);
                    await Task.Delay(500);
                    Require(view.SquircleActive&&!view.FluidActive,"the option turns the squircle island on in place of the Fluid dress");
                    Require(Math.Abs(view.Shape.Width-DynamicIslandLayout.SquircleMediaWidth)<.01&&Math.Abs(view.Shape.Height-DynamicIslandLayout.CompactHeight)<.01,"the folded pill has the squircle size: "+view.Shape);
                    Require(!(view.Surface.Clip is RectangleGeometry)&&view.Surface.Clip!=null,"the pill is cut by the squircle outline");
                    Require(view.PillTitle==media.Title,"the pill shows the title");
                    var rim=view.SquircleRim as SolidColorBrush;
                    Require(rim!=null&&rim.Color.R>rim.Color.G+40&&rim.Color.R>rim.Color.B+40,"the rim takes the red of the cover");
                    var host=view.Content as FrameworkElement;
                    Program.Save(host,360,40,Path.Combine(AppLog.Data,"squircle-island-media.png"));
                    // Opening: frames across the grow, then the card.
                    view.SetExpanded(true);
                    int waited=0;
                    foreach(int at in new[]{30,70,120,180,260,400})
                    {
                        await Task.Delay(at-waited);waited=at;
                        Program.Save(host,360,220,Path.Combine(AppLog.Data,"squircle-island-open-"+at+".png"));
                    }
                    await Task.Delay(500);
                    Require(!view.Animating&&view.Shape.Height>100,"the card settles");
                    Require(view.Shape.Bottom<=DynamicIslandLayout.MaximumHeight+.01,"the card stays inside the host window");
                    Program.Save(host,360,220,Path.Combine(AppLog.Data,"squircle-island-expanded.png"));
                    view.SetExpanded(false);
                    waited=0;
                    foreach(int at in new[]{20,60,110,170})
                    {
                        await Task.Delay(at-waited);waited=at;
                        Require(view.Surface.Clip!=null&&!(view.Surface.Clip is RectangleGeometry),"the closing shape keeps the squircle outline at "+at+" ms");
                        Program.Save(host,360,220,Path.Combine(AppLog.Data,"squircle-island-close-"+at+".png"));
                    }
                    await Task.Delay(530);
                    Require(!view.Animating&&Math.Abs(view.Shape.Width-DynamicIslandLayout.SquircleMediaWidth)<.01,"closing settles on the folded pill");
                    // Charging over music: the music splits off into the small island on the left.
                    view.ShowFeedback("charge",80,false);
                    waited=0;
                    foreach(int at in new[]{40,100,180,300})
                    {
                        await Task.Delay(at-waited);waited=at;
                        Program.Save(host,360,40,Path.Combine(AppLog.Data,"squircle-island-split-"+at+".png"));
                    }
                    await Task.Delay(400);
                    Require(view.SatelliteVisible,"charging over music shows the small island");
                    var small=view.SatelliteShape;var main=view.Shape;
                    Require(small.Right<=main.Left-DynamicIsland.SatelliteGap+.5&&small.Height==DynamicIslandLayout.CompactHeight,"the small island sits left of the main one: "+small+" / "+main);
                    Require(view.BackdropClip.HasValue&&view.BackdropClip.Value.Contains(small)&&view.BackdropClip.Value.Contains(main),"the input area holds both islands");
                    Program.Save(host,360,40,Path.Combine(AppLog.Data,"squircle-island-charging.png"));
                    await Task.Delay(3400);
                    Require(!view.FeedbackVisible,"the charge feedback ends");
                    await Task.Delay(500);
                    Require(!view.SatelliteVisible&&view.BackdropClip.HasValue&&!view.BackdropClip.Value.Contains(small),"the small island goes back into the main one and leaves the input area");
                    // Turning the option off returns the classic island.
                    Preferences.Set(p=>p.SquircleIsland=false);
                    view.Present(media,"Phone speaker",screen,dpi);
                    await Task.Delay(700);
                    Require(!view.SquircleActive&&Math.Abs(view.Shape.Width-DynamicIslandLayout.MediaWidth)<.01&&view.Surface.Clip is RectangleGeometry,"turning it off returns the classic capsule");
                    view.Suspend();
                    AppLog.Json("squircle-island-probe.json",new {passed=true});
                    result=0;
                }
                catch(Exception error){AppLog.Json("squircle-island-probe.json",new {passed=false,error=error.ToString(),island=view==null?null:view.Inspect()});}
                finally{Preferences.Set(p=>p.SquircleIsland=oldSquircle);if(view!=null)view.Close();if(motion!=null)motion.Dispose();app.Shutdown(result);}
            }),DispatcherPriority.ApplicationIdle);
            app.Run();return result;
        }
    }
}
