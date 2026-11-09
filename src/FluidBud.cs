using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI's arrival: a drop is pulled up out of the bar, tears off, then swells until it is the panel.
    // The panel's real shape is what moves. Its backdrop, its window and its picture all follow one outline, so nothing
    // is scaled or squashed. The window is stretched down to the bar's edge for the length of the motion, so the drop
    // really starts in the bar; the neck thins and snaps, a small residue falls back, and the loose drop swells on a spring.
    // Closing runs the shape back into the bar. The bar itself is drawn down a little while the drop is pulled.
    internal sealed class FluidBud
    {
        const double Tear=.2,Swell=.2,Orb=36,Lift=14,CloseTime=.2;
        readonly GlassWindow window;
        readonly Stopwatch watch=new Stopwatch();
        readonly RectangleGeometry clip=new RectangleGeometry();
        bool running,closing,hooked;
        double anchor;
        bool fromTop;
        Action finished;
        // The bar moves with the drop: drawn down a little as the drop is pulled up, settling back when it lets go.
        internal static event Action<double> BarTug;
        double extra;bool grown;Native.RectI original;
        static void Tug(double pull){var handler=BarTug;if(handler!=null)handler(pull);}
        internal FluidBud(GlassWindow owner){window=owner;}
        internal bool Active {get{return running;}}

        internal void Open(Point at,bool top)
        {
            Begin(at,top,false,null);
        }
        internal void Close(Point at,bool top,Action done)
        {
            Begin(at,top,true,done);
        }

        void Begin(Point at,bool top,bool close,Action done)
        {
            Cancel();
            fromTop=top;
            GrowToBar();
            anchor=at.X;
            closing=close;
            finished=done;
            running=true;
            watch.Restart();
            Apply(0);
            if(!hooked){hooked=true;CompositionTarget.Rendering+=Frame;}
        }

        // The panel floats above the bar. For the drop to come out of the bar, the window is made as tall as the gap and
        // the panel keeps its place; the gap is transparent and only the drop passes through it.
        void GrowToBar()
        {
            extra=0;grown=false;
            var handle=new WindowInteropHelper(window).Handle;
            Native.RectI rect;
            if(handle==IntPtr.Zero||!Native.GetWindowRect(handle,out rect))return;
            double scale=Math.Max(96,Native.GetDpiForWindow(handle))/96.0;
            var screen=System.Windows.Forms.Screen.FromHandle(handle).Bounds;
            double gap=(fromTop?rect.Top-screen.Top:screen.Bottom-rect.Bottom)/scale-14;
            if(gap<6)return;
            extra=Math.Min(140,gap);
            int pixels=(int)Math.Round(extra*scale);
            original=rect;
            window.Surface.Margin=new Thickness(0,fromTop?extra:0,0,fromTop?0:extra);
            Native.SetWindowPos(handle,IntPtr.Zero,rect.Left,fromTop?rect.Top-pixels:rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top+pixels,0x14);
            grown=true;
            window.UpdateLayout();
        }
        void ShrinkBack()
        {
            if(!grown)return;
            grown=false;extra=0;
            window.Surface.Margin=new Thickness(0);
            var handle=new WindowInteropHelper(window).Handle;
            if(handle!=IntPtr.Zero)Native.SetWindowPos(handle,IntPtr.Zero,original.Left,original.Top,original.Right-original.Left,original.Bottom-original.Top,0x14);
        }

        void Frame(object sender,EventArgs e)
        {
            double total=closing?CloseTime:Tear+Swell;
            double time=watch.Elapsed.TotalSeconds;
            if(time>=total)
            {
                var done=finished;
                bool wasClosing=closing;
                finished=null;
                Stop(false);
                if(done!=null)done();
                // After the window is hidden the normal shape comes back; a new motion started by done keeps its own.
                if(!running)Restore();
                return;
            }
            Apply(time);
        }

        static double Smooth(double a,double b,double x)
        {
            double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));
            return t*t*(3-2*t);
        }

        // The shape at a moment. It is built as if the bar were below the panel, in window coordinates with the bar at the
        // bottom edge; a window that opens downward is flipped afterwards.
        void Apply(double time)
        {
            double width=window.Surface.ActualWidth,height=window.Surface.ActualHeight;
            if(width<2||height<2)return;
            double panelHeight=height;
            double total=panelHeight+extra;
            double orb=Math.Min(Orb,Math.Min(width,panelHeight));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            Rect full=new Rect(0,0,width,panelHeight);
            Rect loose=new Rect(cx-orb/2,panelHeight-Lift-orb,orb,orb);
            if(closing)
            {
                double v=Math.Max(0,Math.Min(1,time/CloseTime));
                if(v<.55)
                {
                    Rect shape=Lerp(loose,full,1-Smooth(0,.55,v));
                    shape.Intersect(full);
                    Tug(0);
                    Place(shape,Math.Max(0,1-v*3.2),shape,total);
                }
                else
                {
                    // The drop falls back into the bar and is gone.
                    double w=(v-.55)/.45,drop=Smooth(0,1,w);
                    double size=Math.Max(1.5,orb*(1-drop*.8)),centerY=loose.Y+orb/2+(total-loose.Y-orb/2)*drop;
                    Rect shape=new Rect(cx-size/2,centerY-size/2,size,size);
                    Tug(2.4*Math.Sin(Math.PI*Math.Min(1,w*1.2)));
                    Place(shape,0,shape,total);
                }
                return;
            }
            if(time<Tear)
            {
                Tearing(time/Tear,cx,width,total,orb,loose);
                return;
            }
            double s=Math.Max(0,Math.Min(1,(time-Tear)/Swell));
            double p=SpringEase.Position(s,.8);
            Rect panel=Lerp(loose,full,p);
            panel.Intersect(full);
            if(panel.IsEmpty)panel=loose;
            Tug(0);
            Place(panel,Math.Max(0,Math.Min(1,(p-.18)/.5)),panel,total);
        }

        // The drop is pulled out of the bar's edge on a neck that thins and snaps; a little residue draws back in.
        void Tearing(double u,double cx,double width,double total,double orb,Rect loose)
        {
            double travel=total-loose.Y-orb/2;
            double rise=SpringEase.Position(u,.55);
            double radius=Math.Max(1.5,orb/2*SpringEase.Position(Math.Min(1,u*1.15),.8));
            // The drop is pulled well above where it settles before the neck lets go, so the tear reads clearly.
            double centerY=total-travel*rise-orb*.7*Math.Sin(Math.PI*Math.Min(1,u/.85))*(1-u*.4);
            double neck=radius*.55*(1-Smooth(.3,.78,u));
            double basis=radius*1.15*(1-Smooth(.5,.92,u));
            double residue=orb*.26*Smooth(0,.25,u)*(1-Smooth(.55,1,u));
            // Each part is merged into one outline; a plain group would cancel them where their windings oppose.
            Geometry group=new EllipseGeometry(new Point(cx,centerY),radius,radius*(1+.28*(1-Smooth(0,.8,u))));
            double bottomY=total;
            if(neck>.6&&basis>.6&&centerY<bottomY-radius)
            {
                var path=new StreamGeometry();
                using(var dc=path.Open())
                {
                    double top=centerY,mid=(centerY+bottomY)/2;
                    dc.BeginFigure(new Point(cx-radius*.9,top),true,true);
                    dc.BezierTo(new Point(cx-neck,mid),new Point(cx-neck,mid),new Point(cx-basis,bottomY),true,true);
                    dc.LineTo(new Point(cx+basis,bottomY),true,true);
                    dc.BezierTo(new Point(cx+neck,mid),new Point(cx+neck,mid),new Point(cx+radius*.9,top),true,true);
                }
                group=Geometry.Combine(group,path,GeometryCombineMode.Union,null);
            }
            if(residue>.8)group=Geometry.Combine(group,new EllipseGeometry(new Point(cx,bottomY),residue,residue),GeometryCombineMode.Union,null);
            double halfWidth=Math.Max(radius,Math.Max(basis,residue));
            double topEdge=Math.Max(0,centerY-radius*1.3);
            Rect region=new Rect(cx-halfWidth,topEdge,Math.Max(2,halfWidth*2),Math.Max(2,bottomY-topEdge));
            Rect circle=new Rect(cx-radius,centerY-radius,radius*2,radius*2);
            // The bar is drawn down as the drop is pulled, then springs back when the neck lets go.
            Tug(u<.62?3.4*Math.Sin(Math.PI/2*Math.Min(1,u/.62)):-1.8*Math.Sin(Math.PI*Math.Min(1,(u-.62)/.38))+3.4*(1-Smooth(.62,.8,u)));
            if(fromTop)
            {
                var flipped=group.Clone();flipped.Transform=new MatrixTransform(1,0,0,-1,0,total);group=flipped;
                region=Flip(region,total);
                circle=Flip(circle,total);
            }
            region.Intersect(new Rect(0,0,width,total));
            Shape(group,circle,region,0);
        }

        static Rect Flip(Rect rect,double total){return new Rect(rect.X,total-rect.Bottom,rect.Width,rect.Height);}
        static Rect Lerp(Rect a,Rect b,double t)
        {
            return new Rect(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,Math.Max(1,a.Width+(b.Width-a.Width)*t),Math.Max(1,a.Height+(b.Height-a.Height)*t));
        }

        // A rounded rectangle outline: the backdrop, the window and the picture all follow it.
        void Place(Rect shape,double content,Rect region,double total)
        {
            if(fromTop){shape=Flip(shape,total);region=Flip(region,total);}
            double radius=Math.Min(window.Radius,Math.Min(shape.Width,shape.Height)/2);
            clip.Rect=shape;
            clip.RadiusX=clip.RadiusY=radius;
            Shape(clip,shape,region,content);
        }

        // Applies an outline: the geometry clips the picture, the rectangle sizes the backdrop and the region the window.
        void Shape(Geometry outline,Rect backdrop,Rect region,double content)
        {
            window.BackdropClip=backdrop;
            window.RegionClip=region;
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=outline;
            var inside=window.Surface.Child;
            if(inside!=null)inside.Opacity=content;
            Native.Round(window,window.Radius);
        }

        void Stop(bool restore)
        {
            if(hooked){hooked=false;CompositionTarget.Rendering-=Frame;}
            running=false;
            watch.Stop();
            Tug(0);
            if(!restore)return;
            Restore();
        }

        void Restore()
        {
            window.BackdropClip=null;
            window.RegionClip=null;
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=null;
            var inside=window.Surface.Child;
            if(inside!=null)inside.Opacity=1;
            ShrinkBack();
            Native.Round(window,window.Radius);
        }

        // Stops wherever it is and puts the window back to its normal shape.
        internal void Cancel()
        {
            bool was=running||hooked||window.RegionClip.HasValue||grown;
            Stop(false);
            finished=null;
            if(was)Restore();
        }
    }
}
