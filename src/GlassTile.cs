using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class GlassOptics
    {
        static readonly Brush lightRim=Rim(false),darkRim=Rim(true),lightDepth=Depth(false),darkDepth=Depth(true);
        static Brush Rim(bool dark)
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(.18,0),EndPoint=new Point(.78,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)202:(byte)242,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)101:(byte)142,234,245,255),.18));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(12,215,231,250),.52));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)33:(byte)58,84,108,142),1));brush.Freeze();return brush;
        }
        static Brush Depth(bool dark)
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};brush.GradientStops.Add(new GradientStop(Colors.Transparent,0));brush.GradientStops.Add(new GradientStop(Colors.Transparent,.64));brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)27:(byte)19,8,16,27),1));brush.Freeze();return brush;
        }
        internal static void Draw(DrawingContext dc,double width,double height,double radius,bool strong=true,bool? darkOverride=null)
        {
            if(!Glass95.Transparent||width<3||height<3)return;bool dark=darkOverride??Glass95.Dark;var box=new Rect(.65,.65,width-1.3,height-1.3);var clip=new RectangleGeometry(box,radius,radius);dc.PushClip(clip);dc.DrawRectangle(dark?darkDepth:lightDepth,null,box);
            // A broad, low-opacity inner reflection plus one sharp top edge reads as thickness.
            var light=new LinearGradientBrush(Color.FromArgb(dark?(byte)25:(byte)38,255,255,255),Colors.Transparent,new Point(0,0),new Point(.25,.72));dc.DrawRoundedRectangle(null,new Pen(light,3),new Rect(1.7,1.7,Math.Max(0,width-3.4),Math.Max(0,height-3.4)),Math.Max(0,radius-1),Math.Max(0,radius-1));
            dc.DrawRoundedRectangle(null,new Pen(dark?darkRim:lightRim,strong?1.15:.8),box,radius,radius);dc.Pop();
        }
    }
    internal sealed class GlassTileBorder : Border
    {
        internal bool Optical=true;
        protected override void OnRender(DrawingContext dc){base.OnRender(dc);if(Optical&&Glass95.Enabled)GlassOptics.Draw(dc,ActualWidth,ActualHeight,CornerRadius.TopLeft,true,true);}
    }
    internal sealed class GlassTileShadow : FrameworkElement
    {
        internal double Radius=30;internal GlassTileShadow(){IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc)
        {
            if(!Glass95.Enabled||!Glass95.Transparent||ActualWidth<3||ActualHeight<3)return;
            for(int i=5;i>=1;i--){var shade=Color.FromArgb(5,2,9,19);var box=new Rect(i*.35,2.5+i*.12,Math.Max(0,ActualWidth-i*.7),Math.Max(0,ActualHeight-1));dc.DrawRoundedRectangle(new SolidColorBrush(shade),null,box,Radius,Radius);}
        }
    }
}
