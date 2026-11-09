using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI's arrival: a drop tears off the bar next to the pointer, then swells until it is the panel.
    // The panel's real shape is what moves. Its backdrop, its window and its picture all follow one outline, so nothing
    // is scaled or squashed. First the drop is pulled up out of the bar's edge on a thinning neck, the neck snaps and
    // a small residue falls back into the bar; then the loose drop swells, on a spring, into the panel.
    // Closing runs the shape back into the bar.
    internal sealed class FluidBud
    {
        const double Tear=.2,Swell=.2,Orb=36,Lift=14,CloseTime=.18;
        readonly GlassWindow window;
        readonly Stopwatch watch=new Stopwatch();
        readonly RectangleGeometry clip=new RectangleGeometry();
        bool running,closing,hooked;
        double anchor;
        bool fromTop;
        Action finished;
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
            anchor=at.X;
            fromTop=top;
            closing=close;
            finished=done;
            running=true;
            watch.Restart();
            Apply(0);
            if(!hooked){hooked=true;CompositionTarget.Rendering+=Frame;}
        }

        void Frame(object sender,EventArgs e)
        {
            double total=closing?CloseTime:Tear+Swell;
            double time=watch.Elapsed.TotalSeconds;
            if(time>=total)
            {
                var done=finished;
                bool wasClosing=closing;
                Stop(!wasClosing);
                finished=null;
                if(done!=null)done();
                return;
            }
            Apply(time);
        }

        static double Smooth(double a,double b,double x)
        {
            double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));
            return t*t*(3-2*t);
        }

        // The shape at a moment, in window coordinates; also sets how much of the content shows.
        void Apply(double time)
        {
            double width=window.Surface.ActualWidth,height=window.Surface.ActualHeight;
            if(width<2||height<2)return;
            double orb=Math.Min(Orb,Math.Min(width,height));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            Rect full=new Rect(0,0,width,height);
            if(closing)
            {
                double v=Math.Max(0,Math.Min(1,time/CloseTime));
                Rect drop=new Rect(cx-orb/2,height-Lift-orb,orb,orb);
                Rect shape=Lerp(drop,full,1-Smooth(0,1,v));
                double shrink=Smooth(.55,1,v);
                if(shrink>0)shape=Scale(shape,1-shrink);
                shape.Intersect(full);
                if(shape.IsEmpty)shape=new Rect(cx-1,height-2,2,2);
                Place(shape,Math.Max(0,1-v*2.6),shape);
                return;
            }
            if(time<Tear)
            {
                Tearing(time/Tear,cx,width,height,orb);
                return;
            }
            double s=Math.Max(0,Math.Min(1,(time-Tear)/Swell));
            double p=SpringEase.Position(s,.8);
            Rect loose=new Rect(cx-orb/2,height-Lift-orb,orb,orb);
            Rect panel=Lerp(loose,full,p);
            panel.Intersect(full);
            if(panel.IsEmpty)panel=loose;
            Place(panel,Math.Max(0,Math.Min(1,(p-.18)/.5)),panel);
        }

        // The drop is pulled out of the bar''s edge on a neck that thins and snaps; a little residue draws back in.
        // Everything is built as if the bar were below; a window that opens downward is flipped afterwards.
        void Tearing(double u,double cx,double width,double height,double orb)
        {
            double travel=orb/2+Lift;
            double rise=SpringEase.Position(u,.55);
            double radius=Math.Max(1.5,orb/2*SpringEase.Position(Math.Min(1,u*1.15),.8));
            // The drop is pulled well above where it settles before the neck lets go, so the tear reads clearly.
            double centerY=height-travel*rise-orb*.7*Math.Sin(Math.PI*Math.Min(1,u/.85))*(1-u*.4);
            double neck=radius*.55*(1-Smooth(.3,.78,u));
            double basis=radius*1.15*(1-Smooth(.5,.92,u));
            double residue=orb*.26*Smooth(0,.25,u)*(1-Smooth(.55,1,u));
            // Each part is merged into one outline; a plain group would cancel them where their windings oppose.
            Geometry group=new EllipseGeometry(new Point(cx,centerY),radius,radius*(1+.28*(1-Smooth(0,.8,u))));
            double bottomY=height;
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
            double left=cx-Math.Max(radius,Math.Max(basis,residue)),right=cx+Math.Max(radius,Math.Max(basis,residue));
            double topEdge=centerY-radius*1.3;
            Rect region=new Rect(left,Math.Max(0,topEdge),Math.Max(2,right-left),Math.Max(2,bottomY-Math.Max(0,topEdge)));
            Rect circle=new Rect(cx-radius,centerY-radius,radius*2,radius*2);
            if(fromTop)
            {
                var flipped=group.Clone();flipped.Transform=new MatrixTransform(1,0,0,-1,0,height);group=flipped;
                region=new Rect(region.X,height-region.Bottom,region.Width,region.Height);
                circle=new Rect(circle.X,height-circle.Bottom,circle.Width,circle.Height);
            }
            region.Intersect(new Rect(0,0,width,height));
            Shape(group,circle,region,0);
        }

        static Rect Lerp(Rect a,Rect b,double t)
        {
            return new Rect(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,Math.Max(1,a.Width+(b.Width-a.Width)*t),Math.Max(1,a.Height+(b.Height-a.Height)*t));
        }
        static Rect Scale(Rect shape,double factor)
        {
            double w=Math.Max(2,shape.Width*factor),h=Math.Max(2,shape.Height*factor);
            return new Rect(shape.X+(shape.Width-w)/2,shape.Y+(shape.Height-h)/2,w,h);
        }

        // A rounded rectangle outline: the backdrop, the window and the picture all follow it.
        void Place(Rect shape,double content,Rect region)
        {
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
            Native.Round(window,window.Radius);
        }

        // Stops wherever it is and puts the window back to its normal shape.
        internal void Cancel()
        {
            bool was=running||hooked||window.RegionClip.HasValue;
            Stop(false);
            finished=null;
            if(was)Restore();
        }
    }
}
