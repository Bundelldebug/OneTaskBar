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
        internal GlassLens(GlassWindow owner):base(owner.Surface)
        {
            window=owner;IsHitTestVisible=false;
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,0));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,.23));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,.62));
            edge.GradientStops.Add(new GradientStop(Colors.Transparent,1));
            rim=new Pen(edge,owner is NowBar?.65:.85);
        }
        internal void Move(Point point){light=point;InvalidateVisual();}
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
            dc.PushClip(shape);dc.DrawRoundedRectangle(null,rim,bounds,radius,radius);dc.Pop();
        }
    }
}