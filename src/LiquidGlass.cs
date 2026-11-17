using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace OneTaskBar
{
    // The optional liquid material (off by default): a live backdrop with a lighter blur, a saturation lift so the
    // colors behind a panel stay vivid, and a very thin tint so most of what is behind shows through.
    // It reuses the existing live backdrop; nothing is captured or redrawn per frame.
    internal static class LiquidGlass
    {
        // Prism numbers (the Prism glass in the One UI Book apps): vibrance 1.35, blur level "high" 2 dp, tint .25 light / .32 dark.
        internal const double Saturation=1.35,PanelBlur=8,DockBlur=6;
        static readonly Brush[] tints=new Brush[10];
        static readonly Brush[] rims=new Brush[2];
        internal static bool Enabled {get{return (Preferences.Current.LiquidGlass||Fluid.Enabled)&&!Performance.Light&&Glass95.Enabled&&Glass95.Transparent;}}
        // Radius for a surface: the liquid radius when the material is on, otherwise what the caller already chose.
        internal static double Radius(double current,bool live,bool dock)
        {
            if(!Enabled||!live)return current;
            return Fluid.Enabled?(dock?10:14):dock?DockBlur:PanelBlur;
        }
        internal static double Boost(bool live){return Enabled&&live?(Fluid.Enabled?1.25:Saturation):1;}
        // level 0 is the thinnest; each step adds tint for a brighter backdrop (see LiquidReadability).
        internal static Brush Tint(bool dark){return Tint(dark,0);}
        internal static Brush Tint(bool dark,int level)
        {
            level=Math.Max(0,Math.Min(4,level));
            // The Fluid UI keeps its warm light on the thinnest glass; the brighter readability steps stay as they were.
            if(Fluid.Enabled&&level==0)return Fluid.Tint(dark);
            int index=(dark?1:0)*5+level;var cached=tints[index];if(cached!=null)return cached;
            // Prism's flat tint with its soft top sheen: light .25 / dark .32, a white lift fading out by 60% of the height.
            int more=level*26;byte baseAlpha=(byte)Math.Min(235,(dark?82:64)+more);
            Color tint=dark?Color.FromRgb(28,28,30):Color.FromRgb(255,255,255);
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            double sheen=dark?.07:.26;
            brush.GradientStops.Add(new GradientStop(Over(tint,baseAlpha,sheen),0));
            brush.GradientStops.Add(new GradientStop(Over(tint,baseAlpha,sheen*.3),.42));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha,tint.R,tint.G,tint.B),.6));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(baseAlpha,tint.R,tint.G,tint.B),1));
            brush.Freeze();return tints[index]=brush;
        }
        // The tint with a white sheen laid over it, as one colour.
        static Color Over(Color tint,byte alpha,double sheen)
        {
            double a=alpha/255.0,total=a+sheen*(1-a);
            if(total<=0)return Colors.Transparent;
            Func<byte,byte> mix=c=>(byte)Math.Round((c*a+255*sheen*(1-a))/total);
            return Color.FromArgb((byte)Math.Round(total*255),mix(tint.R),mix(tint.G),mix(tint.B));
        }
    }
}
