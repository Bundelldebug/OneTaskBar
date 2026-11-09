using System;
using System.Windows.Media;

namespace OneTaskBar
{
    // The Fluid UI (10.0 beta), the interface version after 9.5. It builds on the 9.5 glass: the liquid material is
    // always on, light pools under the glass in a glow color, rims catch that light, and panels move like a spring.
    internal static class Fluid
    {
        internal const int Version=100;
        // 10.0.0a is a beta of the interface; the label shows beside the version number.
        internal const bool Beta=true;
        internal static bool Enabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.UIVersion>=Version;}}

        // The glow behind the glass: a preset, a color of the owner's choice, or none.
        internal const int GlowNone=5,GlowCustom=6;
        internal static readonly string[] GlowNames={"주황","분홍","보라","파랑","초록","없음","직접 선택"};
        static readonly Color[] glowColors={Color.FromRgb(0xFF,0x8A,0x3D),Color.FromRgb(0xFF,0x5C,0x9E),Color.FromRgb(0x9A,0x6B,0xFF),Color.FromRgb(0x3D,0x9B,0xFF),Color.FromRgb(0x3D,0xD6,0x8C)};

        internal static int GlowChoice
        {
            get{int value=Preferences.Current.FluidGlow;return value<0||value>GlowCustom?0:value;}
        }
        internal static bool GlowOn {get{return GlowChoice!=GlowNone;}}
        internal static Color GlowColor
        {
            get
            {
                int choice=GlowChoice;
                if(choice==GlowCustom)
                {
                    Color custom;
                    return DockAppearance.TryColor(Preferences.Current.FluidGlowColor,out custom)?Color.FromRgb(custom.R,custom.G,custom.B):glowColors[0];
                }
                return choice<glowColors.Length?glowColors[choice]:glowColors[0];
            }
        }
        // Panels, the island and the preview rebuild when this changes, like they do for a new interface version.
        internal static int StyleKey
        {
            get
            {
                var color=GlowColor;
                return Preferences.Current.UIVersion*100000+(GlowOn?(color.R<<16|color.G<<8|color.B)%99991+1:0);
            }
        }
        // The glow at a given strength; none gives a fully clear color so every gradient keeps its shape.
        internal static Color Glow(int alpha)
        {
            var color=GlowColor;
            return Color.FromArgb(GlowOn?(byte)Math.Max(0,Math.Min(255,alpha)):(byte)0,color.R,color.G,color.B);
        }
        // The glow lightened toward white, for rims and the pointer light, so a deep color still reads as light.
        internal static Color Pale(int alpha)
        {
            var color=GlowColor;
            return Color.FromArgb(GlowOn?(byte)Math.Max(0,Math.Min(255,alpha)):(byte)0,(byte)(color.R+(255-color.R)*.45),(byte)(color.G+(255-color.G)*.45),(byte)(color.B+(255-color.B)*.45));
        }

        static readonly Brush[] tints=new Brush[2];
        static int tintKey;
        // Thin glass whose top is clear and whose lower part holds the glow with a cool lilac band above it.
        internal static Brush Tint(bool dark)
        {
            int key=StyleKey;
            if(key!=tintKey){tints[0]=tints[1]=null;tintKey=key;}
            int index=dark?1:0;
            var cached=tints[index];
            if(cached!=null)return cached;
            var brush=new LinearGradientBrush {StartPoint=new System.Windows.Point(.1,0),EndPoint=new System.Windows.Point(.9,1)};
            if(dark)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(74,24,26,42),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(82,44,34,70),.5));
                brush.GradientStops.Add(new GradientStop(GlowOn?Glow(165):Color.FromArgb(90,40,44,66),1));
            }
            else
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(96,255,255,255),0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(92,240,232,252),.5));
                brush.GradientStops.Add(new GradientStop(GlowOn?Pale(175):Color.FromArgb(100,246,244,252),1));
            }
            brush.Freeze();
            tints[index]=brush;
            return brush;
        }
    }
}
