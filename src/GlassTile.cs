using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class GlassOptics
    {
        static readonly Brush lightRim=Rim(false),darkRim=Rim(true),lightDepth=Depth(false),darkDepth=Depth(true);
        static readonly Pen lightInner=Inner(false),darkInner=Inner(true),lightStrong=FrozenPen(lightRim,.85),darkStrong=FrozenPen(darkRim,.85),lightSoft=FrozenPen(lightRim,.65),darkSoft=FrozenPen(darkRim,.65);
        static Pen FrozenPen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
        static Pen Inner(bool dark){var brush=new LinearGradientBrush(Color.FromArgb(dark?(byte)23:(byte)29,255,255,255),Colors.Transparent,new Point(0,0),new Point(.25,.72));brush.Freeze();return FrozenPen(brush,2.4);}
        static Brush Rim(bool dark)
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(.18,0),EndPoint=new Point(.78,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)155:(byte)180,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)72:(byte)92,234,245,255),.18));
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
            dc.DrawRoundedRectangle(null,dark?darkInner:lightInner,new Rect(1.7,1.7,Math.Max(0,width-3.4),Math.Max(0,height-3.4)),Math.Max(0,radius-1),Math.Max(0,radius-1));
            dc.DrawRoundedRectangle(null,dark?(strong?darkStrong:darkSoft):(strong?lightStrong:lightSoft),box,radius,radius);dc.Pop();
        }
    }
    internal sealed class GlassTileBorder : Border
    {
        internal bool Optical=true;
        RadialGradientBrush reflection;
        Point light=new Point(.3,.12);
        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if(!Optical||!Glass95.Enabled||!Motion.Enabled||!Preferences.Current.Hover||ActualWidth<1||ActualHeight<1)return;
            var point=e.GetPosition(this);var next=new Point(point.X/ActualWidth,point.Y/ActualHeight);
            if(Math.Abs(next.X-light.X)+Math.Abs(next.Y-light.Y)<.05)return;
            light=next;InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if(!Optical||!Glass95.Enabled)return;
            GlassOptics.Draw(dc,ActualWidth,ActualHeight,CornerRadius.TopLeft,true,true);
            if(!Glass95.Transparent||ActualWidth<3||ActualHeight<3)return;
            if(reflection==null){reflection=new RadialGradientBrush(Color.FromArgb(10,255,255,255),Colors.Transparent){RadiusX=.75,RadiusY=.8};}
            reflection.Center=reflection.GradientOrigin=light;
            var box=new Rect(1.5,1.5,ActualWidth-3,ActualHeight-3);
            dc.DrawRoundedRectangle(reflection,null,box,Math.Max(0,CornerRadius.TopLeft-1.5),Math.Max(0,CornerRadius.TopLeft-1.5));
        }
    }
    internal sealed class GlassTileShadow : FrameworkElement
    {
        static readonly Brush shade=Paint.Brush("#05020913");
        internal double Radius=30;internal GlassTileShadow(){IsHitTestVisible=false;}
        protected override void OnRender(DrawingContext dc)
        {
            if(!Glass95.Enabled||!Glass95.Transparent||ActualWidth<3||ActualHeight<3)return;
            for(int i=5;i>=1;i--){var box=new Rect(i*.35,2.5+i*.12,Math.Max(0,ActualWidth-i*.7),Math.Max(0,ActualHeight-1));dc.DrawRoundedRectangle(shade,null,box,Radius,Radius);}
        }
    }
}
