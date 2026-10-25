using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OneTaskBar
{
    // Surface-relative light, clipped to the existing geometry. No second window or captured backdrop.
    internal sealed class GlassLens : Adorner
    {
        readonly GlassWindow window;Point light=new Point(.32,.12);internal bool Active;
        internal GlassLens(GlassWindow owner):base(owner.Surface){window=owner;IsHitTestVisible=false;}
        internal void Move(Point point){light=point;InvalidateVisual();}
        protected override void OnRender(DrawingContext dc)
        {
            double w=AdornedElement.RenderSize.Width,h=AdornedElement.RenderSize.Height;if(!Active||w<2||h<2)return;bool dark=window.Dark;double radius=window.Surface.CornerRadius.TopLeft;
            var bounds=new Rect(.55,.55,Math.Max(0,w-1.1),Math.Max(0,h-1.1));var shape=new RectangleGeometry(bounds,radius,radius);dc.PushClip(shape);
            var glow=new RadialGradientBrush {Center=new Point(light.X*.6+.12,light.Y*.35),GradientOrigin=new Point(light.X*.6+.12,light.Y*.35),RadiusX=.9,RadiusY=.55};glow.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)15:(byte)26,255,255,255),0));glow.GradientStops.Add(new GradientStop(Colors.Transparent,1));dc.DrawRectangle(glow,null,bounds);
            // A single fading inner contour avoids the bright double outline on compact media cards.
            var edge=new LinearGradientBrush {StartPoint=new Point(light.X*.5,0),EndPoint=new Point(.65+light.X*.3,1)};edge.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)190:(byte)230,255,255,255),0));edge.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)65:(byte)94,236,244,255),.23));edge.GradientStops.Add(new GradientStop(Colors.Transparent,.62));edge.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)30:(byte)55,90,112,146),1));dc.DrawRoundedRectangle(null,new Pen(edge,window is NowBar?.7:1.05),bounds,radius,radius);
            dc.Pop();
        }
    }
}
