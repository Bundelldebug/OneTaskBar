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
        internal const double Saturation=1.65,PanelBlur=22,DockBlur=16;
        static readonly Brush[] tints=new Brush[10];
        static readonly Brush[] rims=new Brush[2];
        internal static bool Enabled {get{return Preferences.Current.LiquidGlass&&!Performance.Light&&Glass95.Enabled&&Glass95.Transparent;}}
        // Radius for a surface: the liquid radius when the material is on, otherwise what the caller already chose.
        internal static double Radius(double current,bool live,bool dock)
        {
            return Enabled&&live?(dock?DockBlur:PanelBlur):current;
        }
        internal static double Boost(bool live){return Enabled&&live?Saturation:1;}
        // level 0 is the thinnest; each step adds tint for a brighter backdrop (see LiquidReadability).
        internal static Brush Tint(bool dark){return Tint(dark,0);}
        internal static Brush Tint(bool dark,int level)
        {
            level=Math.Max(0,Math.Min(4,level));
            int index=(dark?1:0)*5+level;var cached=tints[index];if(cached!=null)return cached;
            // Thin: the top is nearly clear and the bottom takes a little more color, as light pools in glass.
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            int more=level*26;
            if(dark){
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(24+more),26,34,48),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(16+more),16,22,32),.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(34+more),10,14,24),1));
            }else{
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(44+more/2),255,255,255),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(28+more/2),236,242,250),.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(46+more/2),214,224,238),1));
            }
            brush.Freeze();return tints[index]=brush;
        }    }
}
