using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OneTaskBar
{
    // The backdrop remains a native live surface. This adorner only lights its rim.
    internal sealed class GlassLens : Adorner
    {
        readonly GlassWindow window;
        readonly LinearGradientBrush edge=new LinearGradientBrush();
        readonly Pen rim;
        RectangleGeometry shape;
        Rect previous;
        double previousRadius;
        bool? palette;
        Point light=new Point(.32,.12);
        internal bool Active;
        // When the liquid material is on, opening a window swells the glass for a moment: the bezel brightens and a
        // highlight travels once around the rim, the way light runs over a lens that is pressed into place.
        const double PulseMilliseconds=720;
        readonly System.Diagnostics.Stopwatch pulseWatch=new System.Diagnostics.Stopwatch();
        readonly LinearGradientBrush sweep=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(1,1)};
        Pen sweepPen;
        bool pulsing;
        double swell;
        internal GlassLens(GlassWindow owner):base(owner.Surface)
        {
            window=owner;IsHitTestVisible=false;
            for(int i=0;i<5;i++)sweep.GradientStops.Add(new GradientStop(Colors.Transparent,i/4.0));
            owner.IsVisibleChanged+=(s,e)=>{if(owner.IsVisible)Pulse();else StopPulse();};
            Unloaded+=(s,e)=>StopPulse();
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,0));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,.23));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,.62));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,1));
            rim=new Pen(edge,owner is NowBar?.65:.85);
        }
        internal void Move(Point point){light=point;InvalidateVisual();}
        internal void Pulse()
        {
            if(!Active||!LiquidGlass.Enabled||!Motion.Enabled||Performance.Light||pulsing)return;
            pulsing=true;pulseWatch.Restart();System.Windows.Media.CompositionTarget.Rendering+=PulseFrame;
        }
        void StopPulse()
        {
            if(!pulsing)return;
            pulsing=false;pulseWatch.Stop();swell=0;System.Windows.Media.CompositionTarget.Rendering-=PulseFrame;InvalidateVisual();
        }
        void PulseFrame(object sender,EventArgs args)
        {
            double t=Math.Min(1,pulseWatch.Elapsed.TotalMilliseconds/PulseMilliseconds);
            if(t>=1){StopPulse();return;}
            // Quick rise, slow settle: the swell peaks early and eases out with no overshoot.
            double eased=1-Math.Exp(-7*t);
            swell=Math.Sin(Math.PI*Math.Min(1,eased*1.15));
            double centre=-.2+1.4*eased,width=.16;
            sweep.GradientStops[0].Offset=0;sweep.GradientStops[1].Offset=Math.Max(0,Math.Min(1,centre-width));
            sweep.GradientStops[2].Offset=Math.Max(0,Math.Min(1,centre));sweep.GradientStops[3].Offset=Math.Max(0,Math.Min(1,centre+width));
            sweep.GradientStops[4].Offset=1;
            byte peak=(byte)Math.Round(210*swell);
            var tone=Color.FromArgb(peak,255,255,255);
            sweep.GradientStops[1].Color=Colors.Transparent;sweep.GradientStops[2].Color=tone;sweep.GradientStops[3].Color=Colors.Transparent;
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            double width=AdornedElement.RenderSize.Width,height=AdornedElement.RenderSize.Height;
            if(!Active||width<2||height<2)return;
            bool dark=window.Dark;
            double radius=window.Surface.CornerRadius.TopLeft;
            var bounds=new Rect(.55,.55,width-1.1,height-1.1);
            if(shape==null||bounds!=previous||radius!=previousRadius){shape=new RectangleGeometry(bounds,radius,radius);shape.Freeze();previous=bounds;previousRadius=radius;}
            if(palette!=dark)
            {
                palette=dark;
                edge.GradientStops[0].Color=Color.FromArgb(dark?(byte)150:(byte)180,255,255,255);
                edge.GradientStops[1].Color=Color.FromArgb(dark?(byte)53:(byte)70,236,244,255);
                edge.GradientStops[2].Color=Colors.Transparent;
                edge.GradientStops[3].Color=Color.FromArgb(dark?(byte)30:(byte)45,90,112,146);
            }
            edge.StartPoint=new Point(light.X*.5,0);
            edge.EndPoint=new Point(.65+light.X*.3,1);
            dc.PushClip(shape);dc.DrawRoundedRectangle(null,rim,bounds,radius,radius);if(LiquidGlass.Enabled)GlassOptics.LiquidEdge(dc,bounds,radius);
            if(pulsing&&LiquidGlass.Enabled)
            {
                if(sweepPen==null)sweepPen=new Pen(sweep,2.2);
                // The swell thickens the rim a little while the highlight passes.
                sweepPen.Thickness=1.6+2.4*swell;
                dc.DrawRoundedRectangle(null,sweepPen,bounds,radius,radius);
            }
            dc.Pop();
        }
    }
}