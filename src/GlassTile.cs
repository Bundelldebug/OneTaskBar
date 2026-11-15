using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class GlassOptics
    {
        static readonly Brush lightRim=Rim(false),darkRim=Rim(true),lightDepth=Depth(false),darkDepth=Depth(true);
        static readonly Pen lightStrong=FrozenPen(lightRim,.85),darkStrong=FrozenPen(darkRim,.85),lightSoft=FrozenPen(lightRim,.65),darkSoft=FrozenPen(darkRim,.65);
        static Pen FrozenPen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
        // Liquid glass catches the light on its upper face. A second painted ring inside the rim read as a gray
        // double outline, so only the sheen is added to the single specular rim.
        static readonly Brush lightSheen=Sheen(false),darkSheen=Sheen(true);
        // A soft sheen across the upper part, where the surface faces the light most directly.
        static Brush Sheen(bool dark)
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(dark?(byte)26:(byte)40,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Colors.Transparent,1));
            brush.Freeze();return brush;
        }
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
        // The optional liquid material: light collects along the thick edge of the glass and fades inward, and a
        // specular glint sits on the upper face. Built from several thin soft strokes, not one hard ring, so it
        // reads as a glow and never as a second outline.
        // The bezel of a lens bends light most at its very edge and not at all toward the middle. The same falloff
        // (1 - sqrt(u(2-u)), u = depth into the bezel) sets how bright each inward ring is, so the edge reads as a
        // thick curved rim rather than a flat outline. The outermost ring is cool and the next warm, a hint of the
        // colour fringe real glass shows.
        const int BezelRings=5;
        const double RingWidth=1.5;
        static readonly Pen[] liquidEdge=LiquidEdgePens();
        static Pen[] LiquidEdgePens()
        {
            var pens=new Pen[BezelRings];
            for(int i=0;i<pens.Length;i++)
            {
                double u=(i+.5)/BezelRings,weight=1-Math.Sqrt(u*(2-u));
                byte strong=(byte)Math.Round(62*weight),weak=(byte)Math.Round(16*weight);
                Color tone=i==0?Color.FromRgb(214,232,255):i==1?Color.FromRgb(255,236,214):Color.FromRgb(255,255,255);
                var brush=new LinearGradientBrush {StartPoint=new Point(.1,0),EndPoint=new Point(.9,1)};
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(strong,tone.R,tone.G,tone.B),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(weak,tone.R,tone.G,tone.B),.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(strong*2/3),tone.R,tone.G,tone.B),1));
                brush.Freeze();
                var pen=new Pen(brush,RingWidth);pen.Freeze();pens[i]=pen;
            }
            return pens;
        }        static readonly Brush liquidGlint=LiquidGlint();
        static Brush LiquidGlint()
        {
            var brush=new RadialGradientBrush(Color.FromArgb(54,255,255,255),Colors.Transparent){Center=new Point(.16,.06),GradientOrigin=new Point(.16,.06),RadiusX=.62,RadiusY=.34};
            brush.Freeze();return brush;
        }
        // The crisp edge of the glass: a thin bright line, strongest where the light comes from (upper left),
        // fading along the sides and returning faintly on the far corner.
        static readonly Pen liquidRim=LiquidRim();
        static Pen LiquidRim()
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(.05,0),EndPoint=new Point(.95,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(170,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(95,255,255,255),.22));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(22,255,255,255),.55));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(44,255,255,255),.85));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(110,255,255,255),1));
            brush.Freeze();
            var pen=new Pen(brush,1.2);pen.Freeze();return pen;
        }
        // Light gathered under the top edge, the thick part of the glass.
        static readonly Brush liquidLip=LiquidLip();
        static Brush LiquidLip()
        {
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(48,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,255,255,255),1));
            brush.Freeze();return brush;
        }
        internal static void LiquidEdge(DrawingContext dc,Rect box,double radius)
        {
            if(box.Width<24||box.Height<24)return;
            for(int i=liquidEdge.Length-1;i>=0;i--){double inset=RingWidth*(i+.5);if(box.Width<=inset*2||box.Height<=inset*2)continue;double r=Math.Max(0,radius-inset);dc.DrawRoundedRectangle(null,liquidEdge[i],new Rect(box.X+inset,box.Y+inset,box.Width-inset*2,box.Height-inset*2),r,r);}
            dc.DrawRectangle(liquidGlint,null,new Rect(box.X,box.Y,box.Width,Math.Min(box.Height*.5,radius*2+30)));
            dc.DrawRectangle(liquidLip,null,new Rect(box.X,box.Y,box.Width,Math.Min(box.Height*.5,16)));
            dc.DrawRoundedRectangle(null,liquidRim,new Rect(box.X+.6,box.Y+.6,box.Width-1.2,box.Height-1.2),Math.Max(0,radius-.6),Math.Max(0,radius-.6));
        }
        internal static void Draw(DrawingContext dc,double width,double height,double radius,bool strong=true,bool? darkOverride=null)
        {
            if(!Glass95.Transparent||width<3||height<3)return;bool dark=darkOverride??Glass95.Dark;var box=new Rect(.65,.65,width-1.3,height-1.3);var clip=new RectangleGeometry(box,radius,radius);dc.PushClip(clip);dc.DrawRectangle(dark?darkDepth:lightDepth,null,box);
            // Optical depth comes from the live contour sampler. A second broad
            // painted stroke produces a gray double outline instead of glass.
            dc.DrawRoundedRectangle(null,dark?(strong?darkStrong:darkSoft):(strong?lightStrong:lightSoft),box,radius,radius);
            if(strong&&width>24&&height>24)
            {
                dc.DrawRectangle(dark?darkSheen:lightSheen,null,new Rect(box.X,box.Y,box.Width,Math.Min(box.Height*.42,radius*1.6+10)));
                if(LiquidGlass.Enabled)LiquidEdge(dc,box,radius);
            }
            dc.Pop();
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
