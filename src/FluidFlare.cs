using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OneTaskBar
{
    // The light of the Fluid UI's arrival, drawn over the panel while its drop becomes a panel: the drop starts white, and
    // a soft white wavefront then runs through the pill from the bar's side, filling it as the picture comes in behind it.
    internal sealed class FluidFlare : Adorner
    {
        Rect shape;
        bool fromTop,visible;
        double wave=-1,white,radius;
        internal FluidFlare(UIElement owner):base(owner)
        {
            IsHitTestVisible=false;
        }
        // shape is in the panel's own space with the bar below; wave runs 0..1 along the opening direction, -1 for none.
        internal void Set(Rect barBelow,bool top,double panelHeight,double front,double whiteness,double corner)
        {
            fromTop=top;
            shape=top?new Rect(barBelow.X,panelHeight-barBelow.Bottom,barBelow.Width,barBelow.Height):barBelow;
            wave=front;
            white=whiteness;
            radius=corner;
            visible=white>.01||wave>=0&&wave<1;
            InvalidateVisual();
        }
        static double Smooth(double a,double b,double x)
        {
            double t=Math.Max(0,Math.Min(1,(x-a)/(b-a)));
            return t*t*(3-2*t);
        }
        protected override void OnRender(DrawingContext dc)
        {
            if(!visible||shape.IsEmpty||shape.Width<2||shape.Height<2)return;
            dc.PushClip(new RectangleGeometry(shape,radius,radius));
            if(white>.01)dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Min(255,215*white),255,255,255)),null,shape);
            if(wave>=0&&wave<1)
            {
                // Distance along the opening direction, 0 at the bar's side. The front is soft and the tail behind it stays light.
                double soft=.3,front=wave*(1+soft)-.02;
                double strength=205*(1-Smooth(.72,1,wave));
                var stops=new GradientStopCollection();
                stops.Add(new GradientStop(Color.FromArgb((byte)(strength*.4),255,255,255),0));
                stops.Add(new GradientStop(Color.FromArgb((byte)(strength*.62),255,255,255),Math.Max(0,Math.Min(1,front-.1))));
                stops.Add(new GradientStop(Color.FromArgb((byte)strength,255,255,255),Math.Max(0,Math.Min(1,front))));
                stops.Add(new GradientStop(Color.FromArgb(0,255,255,255),Math.Max(0,Math.Min(1,front+soft))));
                var band=new LinearGradientBrush(stops,fromTop?new Point(0,0):new Point(0,1),fromTop?new Point(0,1):new Point(0,0));
                dc.DrawRectangle(band,null,shape);
            }
            dc.Pop();
        }
    }
}
