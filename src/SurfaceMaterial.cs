using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // Only the two explicitly customized surfaces participate. Existing panel
    // palettes and each surface's default material remain the fallback.
    internal static class SurfaceMaterial
    {
        static readonly string[] keys=new string[2];
        static readonly Brush[] brushes=new Brush[2];
        internal static int Clamp(int value,int maximum){return Math.Max(0,Math.Min(maximum,value));}
        static bool TryColor(string value,out Color color)
        {
            color=Colors.Transparent;
            try{if(String.IsNullOrWhiteSpace(value))return false;color=(Color)ColorConverter.ConvertFromString(value);return true;}
            catch(FormatException){return false;}catch(NotSupportedException){return false;}
        }
        internal static void Normalize(PreferenceData data)
        {
            Color color;
            if(!TryColor(data.DockMaterialColor,out color))data.DockMaterialColor="#FF52616D";
            if(!TryColor(data.StartMaterialColor,out color))data.StartMaterialColor="#FF3D4854";
            data.DockMaterialOpacity=Clamp(data.DockMaterialOpacity,100);
            data.StartMaterialOpacity=Clamp(data.StartMaterialOpacity,100);
            data.DockMaterialBlur=Clamp(data.DockMaterialBlur,40);
            data.StartMaterialBlur=Clamp(data.StartMaterialBlur,40);
        }
        internal static bool Enabled(bool dock)
        {
            return !SystemParameters.HighContrast&&(dock?Preferences.Current.DockMaterialCustom:Preferences.Current.StartMaterialCustom);
        }
        internal static int Blur(bool dock){return dock?Preferences.Current.DockMaterialBlur:Preferences.Current.StartMaterialBlur;}
        internal static Brush Tint(bool dock,bool opaque)
        {
            int index=dock?0:1;
            var data=Preferences.Current;
            string value=dock?data.DockMaterialColor:data.StartMaterialColor;
            int opacity=opaque?100:dock?data.DockMaterialOpacity:data.StartMaterialOpacity;
            string key=value+"|"+opacity;
            if(keys[index]==key&&brushes[index]!=null)return brushes[index];
            Color color;TryColor(value,out color);
            color.A=(byte)Math.Round(opacity*2.55);
            var brush=new SolidColorBrush(color);brush.Freeze();
            keys[index]=key;brushes[index]=brush;
            return brush;
        }
    }
}
