using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OneTaskBar
{
    // One sheet of glass that changes shape, as the panels do in the One UI recordings. The outline moves on a spring while
    // pictures and content fade on the same glass. The Dex panels use it to arrive, to leave, and for the quick settings tile
    // that grows into its page and shrinks back onto the tile.
    internal sealed class PanelMorph
    {
        internal const int PageMilliseconds=320,AppearMilliseconds=230;
        sealed class Picture : Adorner
        {
            readonly ImageSource image;internal Rect Place;internal double Corner;
            internal Picture(UIElement surface,ImageSource source,Rect at):base(surface)
            {
                image=source;Place=at;IsHitTestVisible=false;
                RenderTransformOrigin=new Point(0,0);
            }
            protected override void OnRender(DrawingContext dc)
            {
                if(Corner>0)dc.PushClip(new RectangleGeometry(Place,Corner,Corner));
                dc.DrawImage(image,Place);
                if(Corner>0)dc.Pop();
            }
        }
        // What a panel shows now, and where it is, for the panel that takes its place.
        internal sealed class Shot
        {
            internal Native.RectI Bounds;internal BitmapSource Image;internal Rect Content;
        }
        sealed class Plan
        {
            internal int Length=PageMilliseconds;internal double Damping=.88;
            // Carry: the content rides on the outline. Scale: and shrinks with it.
            internal bool Carry,Scale,FadeGlass,Legacy;
            internal double ContentFrom=-1,ContentTo=1;
            internal Shot Old;internal double OldCorner;internal Action Done;
            internal FrameworkElement[] Late;
            internal Border Flyer;internal BitmapSource FlyImage;internal Rect FlyFrom,FlyTo;internal double CornerFrom,CornerTo;
            internal Image Under;
        }
        static BitmapSource Snap(FrameworkElement element,double scale)
        {
            var drawing=new DrawingVisual();
            using(var dc=drawing.RenderOpen())dc.DrawRectangle(new VisualBrush(element){Stretch=Stretch.Fill},null,new Rect(0,0,element.ActualWidth,element.ActualHeight));
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*scale),(int)Math.Ceiling(element.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);
            bitmap.Render(drawing);bitmap.Freeze();
            return bitmap;
        }
        internal static Shot Capture(GlassWindow window,FrameworkElement without=null)
        {
            var result=new Shot();
            var handle=new WindowInteropHelper(window).Handle;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out result.Bounds))return null;
            var inside=window.Surface.Child as FrameworkElement;
            if(inside==null||inside.ActualWidth<1||inside.ActualHeight<1)return result;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            var at=inside.TranslatePoint(new Point(),window.Surface);
            result.Content=new Rect(at.X,at.Y,inside.ActualWidth,inside.ActualHeight);
            double before=without!=null?without.Opacity:1;
            if(without!=null)without.Opacity=0;
            result.Image=Snap(inside,scale);
            if(without!=null)without.Opacity=before;
            return result;
        }

        readonly GlassWindow window;readonly FrameworkElement child;readonly IntPtr handle;readonly Plan plan;
        readonly Stopwatch watch=new Stopwatch();readonly RectangleGeometry outline=new RectangleGeometry(),flyClip=new RectangleGeometry();
        readonly ScaleTransform childScale=new ScaleTransform(1,1),pictureScale=new ScaleTransform(1,1),carryScale=new ScaleTransform(1,1),flyScale=new ScaleTransform(1,1);
        readonly TranslateTransform carryMove=new TranslateTransform(),flyMove=new TranslateTransform();
        readonly Native.RectI target;
        readonly double dpiScale=1;readonly bool resizedHost,shapeMoves,gpuBackdrop;
        Rect from,to,union;Thickness margin;Transform childTransform;Point childOrigin;Picture picture,cardPicture;bool done,leaving;int length;Action after;
        Transform flyTransform;Geometry flyOldClip;
        double nowK,nowFade=1,leaveK=1,leaveFade=1,lastT=-1;
        HorizontalAlignment across;VerticalAlignment down;object width,height;
        internal static bool Running(GlassWindow window){return window.Reshape!=null;}
        internal static bool Opens(GlassWindow window){return window is QuickWindow||window is NotificationWindow||window is CalendarWindow||window is DetailWindow||window is DrawerWindow||window is StartWindow||window is SearchWindow;}
        // A panel taking over from another one.
        internal static void Replace(GlassWindow window,Shot old)
        {
            if(old==null){window.Pop();return;}
            Start(window,old.Bounds,new Plan {Old=old,Legacy=true});
        }
        // A panel arriving by itself: the glass settles into place and fades up.
        internal static void Appear(GlassWindow window)
        {
            var handle=new WindowInteropHelper(window).Handle;Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            int insetX=(rect.Right-rect.Left)*3/100,insetY=(rect.Bottom-rect.Top)*4/100;
            var start=new Native.RectI {Left=rect.Left+insetX,Right=rect.Right-insetX,Top=window.PopFromTop?rect.Top:rect.Top+insetY*2,Bottom=window.PopFromTop?rect.Bottom-insetY*2:rect.Bottom};
            Start(window,start,new Plan {Length=AppearMilliseconds,Legacy=true,FadeGlass=true,ContentFrom=.05});
        }
        // How each Dex panel comes and goes, measured from the One UI 9.5 Dex recording:
        // - App drawer & Start spring up from their taskbar key (26% -> 100%) with scaling content.
        // - Finder rises smoothly (+14 px) with a subtle scale (96% -> 100%) and crisp fade.
        // - Calendar rises smoothly (+18 px) with subtle scale (95% -> 100%).
        // - Quick Settings & Notifications anchor to the top bar, softly unfold (92% -> 100%) and settle with header/footer slightly staggered.
        static Plan Spec(GlassWindow window,Native.RectI rect,double scale,bool opening,out Native.RectI start)
        {
            start=rect;int w=rect.Right-rect.Left,h=rect.Bottom-rect.Top;
            if(window is DrawerWindow||window is StartWindow)
            {
                int small=w*26/100,tall=h*26/100,center=rect.Left+w/2;
                try
                {
                    var key=DockHost.PlanFor(window);
                    if(key!=null)center=(key.Source.Left+key.Source.Right)/2;
                }
                catch(Exception error){AppLog.Once("dex-panel-key",error.GetType().Name);}
                int left=Math.Max(rect.Left,Math.Min(rect.Right-small,center-small/2));
                start=new Native.RectI {Left=left,Right=left+small,Top=rect.Bottom-tall,Bottom=rect.Bottom};
                return new Plan {Length=opening?340:170,Damping=.85,Carry=true,Scale=true,FadeGlass=true,ContentFrom=0,ContentTo=.38};
            }
            int unit=(int)Math.Round(scale);
            if(window is SearchWindow)
            {
                int insetX=w*2/100,rise=(opening?14:8)*unit;
                start=new Native.RectI {Left=rect.Left+insetX,Right=rect.Right-insetX,Top=rect.Top+rise,Bottom=rect.Bottom};
                return new Plan {Length=opening?210:140,Damping=.94,Carry=true,Scale=true,FadeGlass=true,ContentFrom=0,ContentTo=.48};
            }
            if(window is CalendarWindow)
            {
                int insetX=w*2/100,rise=(opening?18:10)*unit;
                start=new Native.RectI {Left=rect.Left+insetX,Right=rect.Right-insetX,Top=rect.Top+rise,Bottom=rect.Bottom+rise-h*4/100};
                return new Plan {Length=opening?240:160,Damping=.92,Carry=true,Scale=true,FadeGlass=true,ContentFrom=0,ContentTo=.40};
            }
            int topInsetX=w*2/100,drop=Math.Max(12*unit,h*8/100);
            start=new Native.RectI {Left=rect.Left+topInsetX,Right=rect.Right-topInsetX,Top=rect.Top,Bottom=rect.Bottom-drop};
            return new Plan {Length=opening?260:160,Damping=.94,Carry=true,Scale=true,FadeGlass=true,ContentFrom=0,ContentTo=.36,Late=opening?window.LateParts:null};
        }
        internal static void Open(GlassWindow window)
        {
            var handle=new WindowInteropHelper(window).Handle;Native.RectI rect,start;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            Start(window,start=rect,Spec(window,rect,scale,true,out start),start);
        }
        internal static void Leave(GlassWindow window,Action hidden)
        {
            var running=window.Reshape;
            // Closed while still arriving: it turns round from where it is.
            if(running!=null&&!running.leaving&&running.plan.Carry&&running.plan.Flyer==null&&running.plan.FlyImage==null){running.Turn(hidden);return;}
            if(running!=null)running.Finish();
            var handle=new WindowInteropHelper(window).Handle;Native.RectI rect,start;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect)){hidden();return;}
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            var plan=Spec(window,rect,scale,false,out start);plan.Done=hidden;
            window.UpdateLayout();
            var motion=new PanelMorph(window,start,plan,true);
            if(motion.done){hidden();return;}
            window.Reshape=motion;
        }
        // The window takes a new height and the glass follows it there.
        internal static void Resize(GlassWindow window,double heightDip)
        {
            var handle=new WindowInteropHelper(window).Handle;Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            if(window.Reshape!=null){window.Reshape.Finish();Native.GetWindowRect(handle,out rect);}
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            int bottom=rect.Top+(int)Math.Round(heightDip*scale);
            Native.SetWindowPos(handle,IntPtr.Zero,rect.Left,rect.Top,rect.Right-rect.Left,bottom-rect.Top,0x14);
            window.Height=heightDip;
            Start(window,rect,new Plan {Length=280,Damping=.95,Carry=true});
        }
        // The tile grows into the card of its page while the panel behind it fades.
        internal static void PageOpen(DetailWindow page,Shot quick,Rect tile)
        {
            page.UpdateLayout();
            var card=page.Sheet;
            var at=card.TranslatePoint(new Point(),page.Surface);
            Start(page,quick.Bounds,new Plan {Length=340,Damping=.92,Carry=true,Old=quick,Under=page.Under,Flyer=card,FlyFrom=tile,FlyTo=new Rect(at.X,at.Y,card.ActualWidth,card.ActualHeight),CornerFrom=Math.Min(tile.Height/2,28),CornerTo=card.CornerRadius.TopLeft,Late=page.LateParts});
        }
        // The card shrinks back onto its tile over the panel coming back underneath.
        internal static void PageBack(QuickWindow quick,Shot page,BitmapSource card,Rect cardRect,double corner,Rect tile)
        {
            Start(quick,page.Bounds,new Plan {Length=310,Damping=.95,Carry=true,Old=page,FlyImage=card,FlyFrom=cardRect,FlyTo=tile,CornerFrom=corner,CornerTo=Math.Min(tile.Height/2,28),ContentFrom=.08,ContentTo=.58});
        }
        // The media card opens into the output list: the glass spreads from the card, the card fades on it, and the dark
        // sheet of the list grows from where the card was.
        internal static void OutputOpen(DetailWindow page,Shot card,double corner)
        {
            page.UpdateLayout();
            var handle=new WindowInteropHelper(page).Handle;Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect)||page.Sheet==null){page.Pop();return;}
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            var sheet=page.Sheet;var at=sheet.TranslatePoint(new Point(),page.Surface);
            var origin=new Rect((card.Bounds.Left-rect.Left)/scale,(card.Bounds.Top-rect.Top)/scale,(card.Bounds.Right-card.Bounds.Left)/scale,(card.Bounds.Bottom-card.Bounds.Top)/scale);
            Start(page,card.Bounds,new Plan {Length=380,Damping=.92,FadeGlass=true,ContentTo=.25,Old=card,OldCorner=corner,Flyer=sheet,FlyFrom=origin,FlyTo=new Rect(at.X,at.Y,sheet.ActualWidth,sheet.ActualHeight),CornerFrom=corner,CornerTo=sheet.CornerRadius.TopLeft,Late=page.LateParts});
        }
        // The glass closes around a rectangle on screen while its content fades.
        internal static void LeaveTo(GlassWindow window,Native.RectI end,Action hidden)
        {
            if(window.Reshape!=null)window.Reshape.Finish();
            window.UpdateLayout();
            var motion=new PanelMorph(window,end,new Plan {Length=260,Damping=.96,Carry=true,FadeGlass=true,ContentFrom=0,ContentTo=.34,Done=hidden},true);
            if(motion.done){hidden();return;}
            window.Reshape=motion;
        }
        internal static BitmapSource Card(FrameworkElement element)
        {
            var source=PresentationSource.FromVisual(element);
            double scale=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformToDevice.M11:1;
            return element.ActualWidth<1||element.ActualHeight<1?null:Snap(element,scale);
        }
        static void Start(GlassWindow window,Native.RectI start,Plan plan,Native.RectI? chosen=null)
        {
            if(window.Reshape!=null)window.Reshape.Finish();
            window.UpdateLayout();
            var motion=new PanelMorph(window,chosen??start,plan,false);
            if(motion.done)return;
            window.Reshape=motion;
        }
        PanelMorph(GlassWindow owner,Native.RectI start,Plan details,bool closing)
        {
            window=owner;plan=details;leaving=closing;length=plan.Length;after=plan.Done;child=window.Surface.Child as FrameworkElement;
            handle=new WindowInteropHelper(window).Handle;
            if(child==null||handle==IntPtr.Zero||!Native.GetWindowRect(handle,out target)){done=true;return;}
            dpiScale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            // A spring that passes its mark needs room beyond the final outline.
            bool overshoot=plan.Damping<.90;
            int roomX=plan.Scale&&overshoot?(target.Right-target.Left)*3/100:0,roomY=plan.Scale&&overshoot?(target.Bottom-target.Top)*3/100:0;
            int left=Math.Min(start.Left,target.Left-roomX),top=Math.Min(start.Top,target.Top-roomY),right=Math.Max(start.Right,target.Right+roomX),bottom=Math.Max(start.Bottom,target.Bottom+roomY);
            resizedHost=left!=target.Left||top!=target.Top||right!=target.Right||bottom!=target.Bottom;
            union=new Rect(0,0,(right-left)/dpiScale,(bottom-top)/dpiScale);
            from=new Rect((start.Left-left)/dpiScale,(start.Top-top)/dpiScale,(start.Right-start.Left)/dpiScale,(start.Bottom-start.Top)/dpiScale);
            to=new Rect((target.Left-left)/dpiScale,(target.Top-top)/dpiScale,(target.Right-target.Left)/dpiScale,(target.Bottom-target.Top)/dpiScale);
            shapeMoves=Math.Abs(from.X-to.X)>.5||Math.Abs(from.Y-to.Y)>.5||Math.Abs(from.Width-to.Width)>.5||Math.Abs(from.Height-to.Height)>.5;
            // The glass fills the whole area and the outline cuts it; the new picture keeps its final size and place.
            margin=child.Margin;childTransform=child.RenderTransform;childOrigin=child.RenderTransformOrigin;across=child.HorizontalAlignment;down=child.VerticalAlignment;
            width=child.ReadLocalValue(FrameworkElement.WidthProperty);height=child.ReadLocalValue(FrameworkElement.HeightProperty);
            child.Width=Math.Max(1,to.Width-margin.Left-margin.Right);child.Height=Math.Max(1,to.Height-margin.Top-margin.Bottom);
            child.HorizontalAlignment=HorizontalAlignment.Left;child.VerticalAlignment=VerticalAlignment.Top;
            child.Margin=new Thickness(to.X+margin.Left,to.Y+margin.Top,0,0);
            if(plan.Carry)
            {
                var ride=new TransformGroup();ride.Children.Add(carryScale);ride.Children.Add(carryMove);
                child.RenderTransformOrigin=new Point(0,0);child.RenderTransform=ride;
            }
            else{child.RenderTransformOrigin=new Point(.5,.5);child.RenderTransform=childScale;}
            if(resizedHost)Native.SetWindowPos(handle,IntPtr.Zero,left,top,right-left,bottom-top,0x14);
            window.UpdateLayout();
            // Drive the live blur backdrop via GPU Composition TransformMatrix instead of calling SetWindowRgn on every frame.
            if(shapeMoves)
            {
                window.BackdropClip=to;window.RegionClip=null;
                Native.Round(window,window.Radius);
                Native.BeginBackdropMotion(window);
                gpuBackdrop=true;
            }
            var layer=AdornerLayer.GetAdornerLayer(window.Surface);
            if(plan.Old!=null&&plan.Old.Image!=null)
            {
                var place=new Rect(from.X+plan.Old.Content.X,from.Y+plan.Old.Content.Y,plan.Old.Content.Width,plan.Old.Content.Height);
                if(plan.Under!=null)
                {
                    plan.Under.Source=plan.Old.Image;plan.Under.Width=place.Width;plan.Under.Height=place.Height;
                    plan.Under.Margin=new Thickness(plan.Old.Content.X-margin.Left,plan.Old.Content.Y-margin.Top,0,0);plan.Under.Visibility=Visibility.Visible;
                }
                else if(layer!=null)
                {
                    picture=new Picture(window.Surface,plan.Old.Image,place) {Corner=plan.OldCorner};
                    pictureScale.CenterX=place.X+place.Width/2;pictureScale.CenterY=place.Y+place.Height/2;
                    if(plan.Legacy)picture.RenderTransform=pictureScale;
                    layer.Add(picture);
                }
            }
            if(plan.FlyImage!=null&&layer!=null)
            {
                cardPicture=new Picture(window.Surface,plan.FlyImage,plan.FlyFrom);
                layer.Add(cardPicture);
            }
            if(plan.Flyer!=null)
            {
                flyTransform=plan.Flyer.RenderTransform;flyOldClip=plan.Flyer.Clip;
                var flight=new TransformGroup();flight.Children.Add(flyScale);flight.Children.Add(flyMove);
                plan.Flyer.RenderTransformOrigin=new Point(0,0);plan.Flyer.RenderTransform=flight;plan.Flyer.Clip=flyClip;
            }
            // ShowAt defers opacity/blur for newly opened Dex panels; non-fading morphs (PageOpen, PageBack, Replace, Resize)
            // take over an already-visible glass sheet and must reveal the surface and backdrop immediately on frame 0.
            if(!plan.FadeGlass){window.Surface.Opacity=1;Native.BlurOpacity(window,1);}
            window.IsVisibleChanged+=VisibilityChanged;
            watch.Start();
            Step(0);
            CompositionTarget.Rendering+=Frame;
        }
        void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e){if(!window.IsVisible)Finish();}
        static double Smooth(double a,double b,double x){double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));return t*t*(3-2*t);}
        static double Mix(double a,double b,double k){return a+(b-a)*k;}
        static Rect Mix(Rect a,Rect b,double k){return new Rect(Mix(a.X,b.X,k),Mix(a.Y,b.Y,k),Math.Max(2,Mix(a.Width,b.Width,k)),Math.Max(2,Mix(a.Height,b.Height,k)));}
        void Turn(Action hidden)
        {
            leaving=true;after=hidden;leaveK=nowK;leaveFade=nowFade;length=Math.Max(90,Math.Min(160,(int)(160*Math.Max(.4,nowK))));
            lastT=-1;
            watch.Restart();
        }
        void Frame(object sender,EventArgs e)
        {
            double t=watch.Elapsed.TotalMilliseconds/length;
            if(t>=1){Finish();return;}
            if(lastT>=0&&t-lastT<.0004)return;
            lastT=t;
            Step(t);
        }
        void Step(double t)
        {
            double k,arrive,glass,late;
            if(leaving)
            {
                double ease=1-Smooth(0,1,t);
                k=leaveK*ease;
                arrive=glass=late=leaveFade*(1-Smooth(0,.90,t));
            }
            else
            {
                k=SpringEase.Position(t,plan.Damping);
                arrive=plan.Legacy?Smooth(plan.ContentFrom>=0?plan.ContentFrom:.32,1,t):plan.ContentFrom>=0?Smooth(plan.ContentFrom,plan.ContentTo,t):1;
                glass=plan.FadeGlass?(plan.Legacy?Smooth(0,.7,t):Smooth(0,Math.Max(.22,plan.ContentTo),t)):1;
                late=Smooth(.22,.82,t);
            }
            nowK=k;nowFade=arrive;
            var shape=shapeMoves?Mix(from,to,k):to;
            shape.Intersect(union);if(shape.IsEmpty)return;
            if(shapeMoves)
            {
                double sx=plan.Scale?shape.Width/to.Width:1,sy=plan.Scale?shape.Height/to.Height:1;
                double radius=window.Radius*(plan.Scale?Math.Max(.55,Math.Min(sx,sy)):1);
                outline.Rect=shape;outline.RadiusX=outline.RadiusY=Math.Min(radius,Math.Min(shape.Width,shape.Height)/2);
                var host=window.Content as UIElement;if(host!=null&&host.Clip!=outline)host.Clip=outline;
                if(gpuBackdrop)Native.TransformBackdropMotion(window,to,shape,dpiScale);
                if(plan.Carry)
                {
                    carryScale.ScaleX=sx;carryScale.ScaleY=sy;
                    carryMove.X=shape.X+margin.Left*sx-(to.X+margin.Left);carryMove.Y=shape.Y+margin.Top*sy-(to.Y+margin.Top);
                }
                else childScale.ScaleX=childScale.ScaleY=.975+.025*arrive;
            }
            else if(!plan.Carry)childScale.ScaleX=childScale.ScaleY=.975+.025*arrive;
            child.Opacity=arrive;
            if(plan.Late!=null)foreach(var part in plan.Late)part.Opacity=late;
            double gone=Smooth(0,.50,t);
            if(picture!=null){picture.Opacity=1-gone;pictureScale.ScaleX=pictureScale.ScaleY=1-.035*gone;}
            if(plan.Under!=null)plan.Under.Opacity=1-gone;
            if(plan.Flyer!=null||cardPicture!=null)
            {
                double fly=SpringEase.Position(t,plan.Flyer!=null?.92:.96);
                var card=Mix(plan.FlyFrom,plan.FlyTo,fly);double corner=Mix(plan.CornerFrom,plan.CornerTo,fly);
                if(plan.Flyer!=null&&plan.FlyTo.Width>=1&&plan.FlyTo.Height>=1)
                {
                    double s=Math.Max(.05,card.Width/plan.FlyTo.Width);
                    flyScale.ScaleX=flyScale.ScaleY=s;flyMove.X=card.X-plan.FlyTo.X;flyMove.Y=card.Y-plan.FlyTo.Y;
                    flyClip.Rect=new Rect(0,0,plan.FlyTo.Width,Math.Max(1,card.Height/s));flyClip.RadiusX=flyClip.RadiusY=corner/s;
                    if(plan.Flyer.Child!=null)plan.Flyer.Child.Opacity=Smooth(.26,.72,t);
                }
                else if(cardPicture!=null)
                {
                    cardPicture.Place=card;cardPicture.Corner=corner;cardPicture.Opacity=1-Smooth(.52,1,t);
                    cardPicture.InvalidateVisual();
                }
            }
            if(plan.FadeGlass){window.Surface.Opacity=glass;Native.BlurOpacity(window,glass);}
        }
        internal void Finish()
        {
            if(done)return;done=true;
            CompositionTarget.Rendering-=Frame;window.IsVisibleChanged-=VisibilityChanged;watch.Stop();
            if(window.Reshape==this)window.Reshape=null;
            // A panel that was leaving is hidden first, so nothing shows at full size on the way out.
            var hidden=leaving?after:null;after=null;
            if(hidden!=null)hidden();
            var layer=AdornerLayer.GetAdornerLayer(window.Surface);
            if(picture!=null){if(layer!=null)layer.Remove(picture);picture=null;}
            if(cardPicture!=null){if(layer!=null)layer.Remove(cardPicture);cardPicture=null;}
            if(plan.Under!=null){plan.Under.Source=null;plan.Under.Visibility=Visibility.Collapsed;}
            if(plan.Flyer!=null)
            {
                plan.Flyer.RenderTransform=flyTransform??Transform.Identity;plan.Flyer.Clip=flyOldClip;
                if(plan.Flyer.Child!=null)plan.Flyer.Child.Opacity=1;
            }
            if(plan.Late!=null)foreach(var part in plan.Late)part.Opacity=1;
            child.Opacity=1;child.RenderTransform=childTransform;child.RenderTransformOrigin=childOrigin;child.Margin=margin;child.HorizontalAlignment=across;child.VerticalAlignment=down;
            Restore(FrameworkElement.WidthProperty,width);Restore(FrameworkElement.HeightProperty,height);
            window.Surface.Opacity=1;Native.BlurOpacity(window,1);
            var host=window.Content as UIElement;if(host!=null&&host.Clip==outline)host.Clip=null;
            window.BackdropClip=null;window.RegionClip=null;
            if(resizedHost)Native.SetWindowPos(handle,IntPtr.Zero,target.Left,target.Top,target.Right-target.Left,target.Bottom-target.Top,0x14);
            if(gpuBackdrop)Native.EndBackdropMotion(window,window.Radius);
            else if(resizedHost)Native.Round(window,window.Radius);
        }
        void Restore(DependencyProperty property,object value)
        {
            if(value==DependencyProperty.UnsetValue)child.ClearValue(property);
            else child.SetValue(property,value);
        }
    }

    internal sealed partial class ShellController
    {
        // Quick settings and its pages are one panel in the 9.5 and Dex styles; the Fluid UI keeps its own unfolding.
        bool PanelsMorph {get{return Motion.Enabled&&(Dex.Enabled||Glass95.Enabled&&!Fluid.Enabled);}}
        internal async void ReturnToQuick(DetailWindow page)
        {
            if(!PanelsMorph||!page.IsVisible)
            {
                page.Hide();
                ToggleQuick(null);
                return;
            }
            bool sheet=Dex.Enabled&&page.Sheet!=null;
            var card=sheet?PanelMorph.Card(page.Sheet):null;
            Rect cardRect=new Rect();
            if(sheet){var at=page.Sheet.TranslatePoint(new Point(),page.Surface);cardRect=new Rect(at.X,at.Y,page.Sheet.ActualWidth,page.Sheet.ActualHeight);}
            var old=PanelMorph.Capture(page,sheet?page.Sheet:null);
            var screen=Forms.Screen.FromHandle(new WindowInteropHelper(page).Handle);
            if(Quick==null)Quick=new QuickWindow(this);
            double scale=MonitorScale(screen,Quick),w=Math.Min(380,screen.Bounds.Width/scale-24),maxH=Math.Max(320,screen.Bounds.Height/scale-(Dex.Enabled?IslandSafeArea.Height+DockSizing.BarSpace*DockSizing.Zoom+20:100));
            Quick.Refresh(Power,Brightness,Audio.Value,Audio.Muted,Audio.Available,Brightness>=0);
            double h=Quick.PreferredHeight(w,maxH);
            bool fromTop=Dex.Enabled&&TopBarFor(screen);
            var position=fromTop?TopBarPopup(screen,scale,w,h,false):DockGeometry.Popup(screen.Bounds,scale,w,h,DockGeometry.Edge);
            Quick.PopFromTop=fromTop;
            Quick.ShowAt(screen.Bounds,position.X,position.Y,w,h);
            Quick.UpdateLayout();
            if(sheet&&card!=null&&old!=null)PanelMorph.PageBack(Quick,old,card,cardRect,page.Sheet.CornerRadius.TopLeft,Quick.PageOrigin(page.PageKey));
            else PanelMorph.Replace(Quick,old);
            page.HideImmediately();
            Quick.Activate();
            await System.Threading.Tasks.Task.Yield();
        }
    }
}
