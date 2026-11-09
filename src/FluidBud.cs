using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI's arrival: a small drop pops out of the bar next to the pointer, then swells until it is the panel.
    // The panel's real shape is what moves. Its backdrop, its window and its picture all follow one rounded rectangle
    // that starts as a circle at the bar's edge and ends as the full panel, so nothing is scaled or squashed.
    // Closing runs the same shape backwards into the bar.
    internal sealed class FluidBud
    {
        const double Pop=.15,Swell=.5,Orb=36,CloseTime=.3;
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
            double total=closing?CloseTime:Pop+Swell;
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

        // The shape at a moment, in window coordinates; also sets how much of the content shows.
        void Apply(double time)
        {
            double width=window.Surface.ActualWidth,height=window.Surface.ActualHeight;
            if(width<2||height<2)return;
            double orb=Math.Min(Orb,Math.Min(width,height));
            double cx=Math.Max(orb/2,Math.Min(width-orb/2,anchor*width));
            Rect full=new Rect(0,0,width,height);
            Rect shape;
            double content;
            if(!closing)
            {
                double u=Math.Min(1,time/Pop);
                // The drop pushes out of the bar a little taller than wide, then rounds off.
                double grow=SpringEase.Position(u,.72),relax=1-u;
                double w=Math.Max(2,orb*grow*(1-.12*relax)),h=Math.Max(2,orb*grow*(1+.24*relax));
                Rect drop=new Rect(cx-w/2,fromTop?0:height-h,w,h);
                double v=Math.Max(0,Math.Min(1,(time-Pop)/Swell));
                double p=u<1?0:SpringEase.Position(v,.84);
                shape=Lerp(drop,full,p);
                content=Math.Max(0,Math.Min(1,(p-.18)/.5));
            }
            else
            {
                double v=Math.Max(0,Math.Min(1,time/CloseTime));
                Rect drop=new Rect(cx-orb/2,fromTop?0:height-orb,orb,orb);
                double shrink=Math.Max(0,Math.Min(1,(v-.55)/.45));
                shape=Lerp(drop,full,1-Ease(v));
                if(shrink>0)shape=Scale(shape,1-shrink);
                content=Math.Max(0,1-v*2.4);
            }
            shape.Intersect(full);
            if(shape.IsEmpty)shape=new Rect(cx-1,fromTop?0:height-2,2,2);
            Place(shape,content);
        }

        static double Ease(double v){return v*v*(3-2*v);}
        static Rect Lerp(Rect a,Rect b,double t)
        {
            return new Rect(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,Math.Max(1,a.Width+(b.Width-a.Width)*t),Math.Max(1,a.Height+(b.Height-a.Height)*t));
        }
        static Rect Scale(Rect shape,double factor)
        {
            double w=Math.Max(2,shape.Width*factor),h=Math.Max(2,shape.Height*factor);
            return new Rect(shape.X+(shape.Width-w)/2,shape.Y+(shape.Height-h)/2,w,h);
        }

        void Place(Rect shape,double content)
        {
            double radius=Math.Min(window.Radius,Math.Min(shape.Width,shape.Height)/2);
            clip.Rect=shape;
            clip.RadiusX=clip.RadiusY=radius;
            window.BackdropClip=shape;
            var host=window.Content as UIElement;
            if(host!=null&&host.Clip!=clip)host.Clip=clip;
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
            var host=window.Content as UIElement;
            if(host!=null)host.Clip=null;
            var inside=window.Surface.Child;
            if(inside!=null)inside.Opacity=1;
            Native.Round(window,window.Radius);
        }

        // Stops wherever it is and puts the window back to its normal shape.
        internal void Cancel()
        {
            bool was=running||hooked;
            Stop(false);
            finished=null;
            if(was||window.BackdropClip.HasValue&&Object.ReferenceEquals((window.Content as UIElement)==null?null:(window.Content as UIElement).Clip,clip))Restore();
        }
    }
}
