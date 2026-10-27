using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class PanelTint
    {
        static string colorKey;
        static readonly Brush[] glass=new Brush[2],solid=new Brush[2];
        internal static string Key {get{return Preferences.Current.PanelsUseDockColor?Preferences.Current.DockBackgroundColor??"default":"off";}}
        internal static bool Enabled {get{return Preferences.Current.PanelsUseDockColor&&!SystemParameters.HighContrast;}}
        internal static Brush Surface(bool dark,bool opaque)
        {
            string key=Preferences.Current.DockBackgroundColor;
            if(colorKey!=key){colorKey=key;Array.Clear(glass,0,glass.Length);Array.Clear(solid,0,solid.Length);}
            int index=dark?1:0;
            var cache=opaque?solid:glass;
            if(cache[index]!=null)return cache[index];
            Color color;DockAppearance.TryColor(key,out color);
            // The tint remains dark enough for the panel's white text in its solid fallback.
            double factor=dark?.18:.34;
            color=Color.FromArgb(opaque?(byte)255:(byte)Math.Round((dark?PanelMaterial.DarkPanelOpacity:PanelMaterial.LightPanelOpacity)*2.55),
                (byte)Math.Round(color.R*factor),(byte)Math.Round(color.G*factor),(byte)Math.Round(color.B*factor));
            Brush brush;
            if(opaque)brush=new SolidColorBrush(color);
            else
            {
                var gradient=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};
                foreach(double offset in new[]{0.0,.48,1})gradient.GradientStops.Add(new GradientStop(color,offset));
                brush=gradient;
            }
            brush.Freeze();return cache[index]=brush;
        }
        internal static void RefreshOpen()
        {
            var app=Application.Current;if(app==null)return;
            foreach(Window window in app.Windows)
            {
                var glassWindow=window as GlassWindow;
                if(glassWindow!=null&&(PanelMaterial.ReadablePanel(window)||PanelMaterial.NotificationPopup(window)||window is HudWindow))
                    glassWindow.RefreshGlassMaterial();
            }
        }
    }
}
