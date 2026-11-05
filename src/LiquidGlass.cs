using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The optional liquid material (off by default): a live backdrop with a lighter blur, a saturation lift so the
    // colors behind a panel stay vivid, and a much thinner tint so most of what is behind shows through.
    // It reuses the existing live backdrop; nothing is captured or redrawn per frame.
    internal static class LiquidGlass
    {
        internal const double Saturation=1.65,PanelBlur=20,DockBlur=14;
        static readonly Brush[] tints=new Brush[2];
        static readonly Brush[] rims=new Brush[2];
        internal static bool Enabled {get{return Preferences.Current.LiquidGlass&&Glass95.Enabled&&Glass95.Transparent;}}
        // Radius for a surface: the liquid radius when the material is on, otherwise what the caller already chose.
        internal static double Radius(double current,bool live,bool dock)
        {
            return Enabled&&live?(dock?DockBlur:PanelBlur):current;
        }
        internal static double Boost(bool live){return Enabled&&live?Saturation:1;}
        internal static Brush Tint(bool dark)
        {
            int index=dark?1:0;var cached=tints[index];if(cached!=null)return cached;
            // Thin: the top is nearly clear and the bottom takes a little more color, as light pools in glass.
            var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
            if(dark){
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(46,30,38,52),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(34,18,24,34),.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(58,12,16,26),1));
            }else{
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(70,255,255,255),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(42,236,242,250),.5));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(64,214,224,238),1));
            }
            brush.Freeze();return tints[index]=brush;
        }
    }
}
