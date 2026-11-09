using System;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI (10.0 beta), the interface version after 9.5. It builds on the 9.5 glass: the liquid material is
    // always on, light pools warm under the glass, rims catch a warm light, and the island moves like a spring.
    internal static class Fluid
    {
        internal const int Version=100;
        internal static bool Enabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.UIVersion>=Version;}}

        static readonly Brush[] tints=new Brush[2];
        // Thin glass whose top is clear and whose lower part holds a warm peach glow with a cool lilac band above it.
        internal static Brush Tint(bool dark)
        {
            int index=dark?1:0;
            var cached=tints[index];
            if(cached!=null)return cached;
            var brush=new LinearGradientBrush {StartPoint=new System.Windows.Point(.1,0),EndPoint=new System.Windows.Point(.9,1)};
            if(dark)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(26,34,38,60),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(24,82,60,120),.55));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(58,176,92,56),1));
            }
            else
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(52,255,255,255),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(34,236,224,250),.55));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(70,255,196,150),1));
            }
            brush.Freeze();
            tints[index]=brush;
            return brush;
        }
    }
}
