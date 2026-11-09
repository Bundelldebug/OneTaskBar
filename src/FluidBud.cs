using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI's arrival, after the reference: a drop hangs from the bar's edge on a thin neck, widens into a pill
    // and fills out into the panel while the neck pinches off. One spring, the same kind the Dynamic Island morphs
    // with, drives the whole shape, so it keeps its speed if it is interrupted. The panel's real outline moves: the
    // backdrop, the window and the picture all follow it, and nothing is scaled or squashed. The bar itself stays still.
    // The window is stretched to the bar's edge for the length of the motion so the drop really starts in the bar.
    internal sealed class FluidBud
    {
        const double Orb=30;
        readonly GlassWindow window;
        readonly SpringValue spring=new SpringValue(.002);
        readonly SpringClock clock=new SpringClock();
        readonly RectangleGeometry body=new RectangleGeometry();
        readonly GeometryGroup outline=new GeometryGroup {FillRule=FillRule.Nonzero};
        bool running,closing,hooked;
        double anchor,extra;
        bool fromTop,grown;
        Action finished;
        Native.RectI original;
        internal FluidBud(GlassWindow owner)
        {
            window=owner;
            outline.Children.Add(body);
        }
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
            // Opening is a lively spring that settles with a small overshoot; closing is quick and does not bounce.
            spring.Omega=close?34:15;
            spring.Damping=close?1:.84;
            roundedOnce=false;
            spring.Value=close?1:0;
            spring.Velocity=0;
            spring.Target=close?0:1;
            clock.Restart();
            Apply(spring.Value);
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
            var rendering=e as RenderingEventArgs;
            if(rendering==null)return;
            spring.Step(clock.Next(rendering.RenderingTime));
            if(closing?spring.Value<=.02:spring.AtRest)
            {
                var done=finished;
                finished=null;
                bool wasClosing=closing;
                Stop();
                // A closed window is restored after it is hidden; an open one lands on its exact shape.
                if(!wasClosing)Restore();
                if(done!=null)done();
                if(wasClosing&&!running)Restore();
                return;
            }
            Apply(spring.Value);
        }

        static double Smooth(double a,double b,double x)
        {
            double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));
            return t*t*(3-2*t);
        }
        static double Mix(double a,double b,double t){return a+(b-a)*t;}

        // The shape at spring position v (0 = a drop at the bar, 1 = the panel). It is built as if the bar were below the
        // panel, with the bar at the bottom edge of the window; a window that opens downward is flipped afterwards.
        void Apply(double v)
        {
            double width=window.Surface.ActualWidth,height=window.Surface.ActualHeight;
            if(width<2||height<2)return;
            double total=height+extra;
            double orb=Math.Min(Orb,Math.Min(width,height));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            // The width leads, the height follows: a round drop, then a wide pill, then the panel.
            double across=Math.Max(0,Math.Min(1.06,v*1.18)),down=Math.Max(0,Math.Min(1.06,v));
            double dropTop=total-orb*.88;
            double left=Mix(cx-orb/2,0,across),right=Mix(cx+orb/2,width,across);
            double top=Mix(dropTop,0,down),bottom=Mix(dropTop+orb,height,down);
            var shape=new Rect(left,top,Math.Max(2,right-left),Math.Max(2,bottom-top));
            shape.Intersect(new Rect(0,0,width,total));
            if(shape.IsEmpty)return;
            double radius=Math.Min(window.Radius,Math.Min(shape.Width,shape.Height)/2);
            body.Rect=shape;
            body.RadiusX=body.RadiusY=radius;
            Rect region=shape;
            // The neck holds the drop to the bar and pinches off as the drop swells.
            double pinch=closing?0:1-Smooth(.08,.55,v);
            while(outline.Children.Count>1)outline.Children.RemoveAt(1);
            if(pinch>.04&&shape.Bottom<total-1)
            {
                double topHalf=Math.Min(shape.Width*.2,7)*pinch,baseHalf=Math.Min(shape.Width*.3,13)*pinch;
                double y0=shape.Bottom-radius*.5,mid=(y0+total)/2;
                var neck=new StreamGeometry();
                using(var dc=neck.Open())
                {
                    dc.BeginFigure(new Point(cx-topHalf,y0),true,true);
                    dc.LineTo(new Point(cx+topHalf,y0),true,false);
                    dc.BezierTo(new Point(cx+topHalf*.5,mid),new Point(cx+baseHalf*.4,mid),new Point(cx+baseHalf,total),true,false);
                    dc.LineTo(new Point(cx-baseHalf,total),true,false);
                    dc.BezierTo(new Point(cx-baseHalf*.4,mid),new Point(cx-topHalf*.5,mid),new Point(cx-topHalf,y0),true,false);
                }
                neck.Freeze();
                outline.Children.Add(neck);
                region=Rect.Union(region,new Rect(cx-baseHalf,y0,baseHalf*2,total-y0));
            }
            // The picture shows once the pill is wide enough to hold it.
            double content=closing?Math.Max(0,Math.Min(1,(v-.1)*2.2)):Smooth(.3,.8,v);
            if(fromTop)
            {
                outline.Transform=new MatrixTransform(1,0,0,-1,0,total);
                region=new Rect(region.X,total-region.Bottom,region.Width,region.Height);
                shape=new Rect(shape.X,total-shape.Bottom,shape.Width,shape.Height);
            }
            else outline.Transform=Transform.Identity;
            region.Intersect(new Rect(0,0,width,total));
            window.BackdropClip=shape;
            window.RegionClip=region;
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=outline;
            var inside=window.Surface.Child;
            if(inside!=null)inside.Opacity=content;
            // Setting the window region is the costly part of a frame; it is only redone when the region moved a whole pixel.
            var cut=new Rect(Math.Round(region.X),Math.Round(region.Y),Math.Round(region.Width),Math.Round(region.Height));
            if(cut!=lastCut||!roundedOnce){lastCut=cut;roundedOnce=true;Native.Round(window,window.Radius);}
        }
        Rect lastCut;bool roundedOnce;

        void Stop()
        {
            if(hooked){hooked=false;CompositionTarget.Rendering-=Frame;}
            running=false;
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
            Stop();
            finished=null;
            if(was)Restore();
        }
    }
}
