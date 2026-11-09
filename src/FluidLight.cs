using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // The Fluid UI's light inside a glass panel: a key light in the upper left, warm light bleeding in from the lower
    // right, a soft light that follows the pointer like a finger-sized lamp, and one sweep of light each time the panel
    // opens. It is a single adorner drawn once; the pointer light moves by editing its brush, and the frame callback
    // only runs while that light is still gliding to the pointer.
    internal sealed class FluidLight : Adorner
    {
        static readonly Brush KeyDark=Radial(.12,-.05,.95,Color.FromArgb(34,255,255,255));
        static readonly Brush KeyLight=Radial(.12,-.05,.95,Color.FromArgb(70,255,255,255));
        static readonly Brush Warm=Radial(.92,1.08,1.05,Color.FromArgb(70,255,154,90));
        readonly GlassWindow window;
        readonly RadialGradientBrush pointer=new RadialGradientBrush();
        readonly GradientStop pointerCore=new GradientStop(Colors.White,0),pointerEdge=new GradientStop(Colors.Transparent,1);
        readonly LinearGradientBrush sweep=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,.55)};
        readonly GradientStop sweepBefore=new GradientStop(Colors.Transparent,-.55),sweepHead=new GradientStop(Colors.Transparent,-.3),sweepAfter=new GradientStop(Colors.Transparent,-.05);
        readonly SpringValue x=new SpringValue(.002) {Omega=16,Damping=.82},y=new SpringValue(.002) {Omega=16,Damping=.82};
        readonly SpringClock clock=new SpringClock();
        bool dark,hooked,present,disposed;
        TimeSpan last=TimeSpan.MinValue;

        internal FluidLight(GlassWindow owner):base(owner.Surface)
        {
            window=owner;
            IsHitTestVisible=false;
            pointer.RadiusX=pointer.RadiusY=.5;
            pointer.GradientStops.Add(pointerCore);
            pointer.GradientStops.Add(pointerEdge);
            pointer.Opacity=0;
            sweep.GradientStops.Add(sweepBefore);
            sweep.GradientStops.Add(sweepHead);
            sweep.GradientStops.Add(sweepAfter);
            x.Value=x.Target=.3;
            y.Value=y.Target=.2;
            SetPointer(.3,.2);
            Palette(window.Dark);
        }

        static Brush Radial(double cx,double cy,double radius,Color color)
        {
            var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B)) {Center=new Point(cx,cy),GradientOrigin=new Point(cx,cy),RadiusX=radius,RadiusY=radius};
            brush.Freeze();
            return brush;
        }

        void Palette(bool isDark)
        {
            dark=isDark;
            pointerCore.Color=Color.FromArgb(isDark?(byte)40:(byte)84,255,244,232);
            pointerEdge.Color=Color.FromArgb(0,255,244,232);
            sweepBefore.Color=Color.FromArgb(0,255,255,255);
            sweepHead.Color=Color.FromArgb(isDark?(byte)60:(byte)110,255,246,236);
            sweepAfter.Color=Color.FromArgb(0,255,255,255);
        }

        void SetPointer(double px,double py)
        {
            var point=new Point(px,py);
            pointer.Center=point;
            pointer.GradientOrigin=point;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double width=AdornedElement.RenderSize.Width,height=AdornedElement.RenderSize.Height;
            if(width<2||height<2)return;
            var rect=new Rect(0,0,width,height);
            double radius=Math.Min(window.Radius,Math.Min(width,height)/2);
            dc.PushClip(new RectangleGeometry(rect,radius,radius));
            dc.DrawRectangle(window.Dark?KeyDark:KeyLight,null,rect);
            dc.DrawRectangle(Warm,null,rect);
            dc.DrawRectangle(pointer,null,rect);
            dc.DrawRectangle(sweep,null,rect);
            dc.Pop();
        }

        // Called with every pointer move over the panel; the light glides after it.
        internal void Move(Point point)
        {
            if(disposed||!Motion.Enabled)return;
            double width=AdornedElement.RenderSize.Width,height=AdornedElement.RenderSize.Height;
            if(width<2||height<2)return;
            if(window.Dark!=dark)Palette(window.Dark);
            x.Target=Math.Max(0,Math.Min(1,point.X/width));
            y.Target=Math.Max(0,Math.Min(1,point.Y/height));
            if(!present)
            {
                present=true;
                pointer.BeginAnimation(Brush.OpacityProperty,new DoubleAnimation(pointer.Opacity,1,TimeSpan.FromMilliseconds(220)));
            }
            Hook();
        }

        internal void Leave()
        {
            if(disposed||!present)return;
            present=false;
            pointer.BeginAnimation(Brush.OpacityProperty,new DoubleAnimation(pointer.Opacity,0,TimeSpan.FromMilliseconds(420)));
        }

        // One diagonal band of light crosses the panel as it opens.
        internal void Sweep()
        {
            if(disposed||!Motion.Enabled||window.Surface.ActualWidth<2)return;
            if(window.Dark!=dark)Palette(window.Dark);
            Slide(sweepBefore,-.55,1.0);
            Slide(sweepHead,-.3,1.25);
            Slide(sweepAfter,-.05,1.5);
        }

        static void Slide(GradientStop stop,double from,double to)
        {
            var move=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(760)) {EasingFunction=new CubicEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
            stop.BeginAnimation(GradientStop.OffsetProperty,move);
        }

        void Hook()
        {
            if(hooked)return;
            hooked=true;
            last=TimeSpan.MinValue;
            clock.Restart();
            CompositionTarget.Rendering+=Frame;
        }

        void Unhook()
        {
            if(!hooked)return;
            hooked=false;
            CompositionTarget.Rendering-=Frame;
        }

        void Frame(object sender,EventArgs e)
        {
            var rendering=e as RenderingEventArgs;
            if(rendering!=null&&rendering.RenderingTime==last)return;
            double seconds=clock.Next(rendering!=null?rendering.RenderingTime:TimeSpan.Zero);
            if(rendering!=null)last=rendering.RenderingTime;
            x.Step(seconds);
            y.Step(seconds);
            SetPointer(x.Value,y.Value);
            if(x.AtRest&&y.AtRest)Unhook();
        }

        internal void Dispose()
        {
            disposed=true;
            Unhook();
            pointer.BeginAnimation(Brush.OpacityProperty,null);
        }
    }
}
