using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    internal static class DockMaterialChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Dock material: "+message);}
        internal static async Task Run()
        {
            int version=Preferences.Current.UIVersion,kind=Preferences.Current.DockBackgroundKind;
            bool blur=Preferences.Current.Blur,solid=Preferences.Current.DockSolidBackground;
            GlassWindow window=null;
            try{
                Preferences.Current.Blur=true;Preferences.Current.DockSolidBackground=false;
                foreach(int ui in ReleaseFeatures.GlassUI?new[]{85,90,95}:new[]{85,90})foreach(bool dark in new[]{false,true})using(Glass95.WithDarkForFixture(dark)){
                    Preferences.Current.UIVersion=ui;Preferences.Current.DockBackgroundKind=0;
                    var label=Paint.TextBlock("오후 4:30",12,Glass95.DockForeground);var glyph=new Glyph("dock-search",22);
                    var content=new StackPanel {Orientation=Orientation.Horizontal};content.Children.Add(glyph);content.Children.Add(label);
                    window=new GlassWindow {DockSurface=true,ShowActivated=false};window.SetMaterial(LightPanel.DockGlass,LightPanel.DockSolid);window.Surface.Child=content;
                    window.ShowAt(System.Windows.Forms.Screen.PrimaryScreen.Bounds,30,30,180,44);window.RefreshGlassMaterial();await Task.Delay(20);
                    Require(window.HasBlur&&Native.RealtimeBackdrop(window)&&Native.BlurOpacity(window)>.99,"default material keeps its live backdrop in every UI/theme");
                    var tint=window.Surface.Background as LinearGradientBrush;
                    Require(window.Surface.Background==LightPanel.DockGlass&&tint!=null&&tint.GradientStops.All(s=>s.Color.A<=77),"light dock never becomes a solid panel and remains at least 70% transparent");
                    if(!dark)Require(tint.GradientStops.Select(s=>s.Color).Distinct().Count()>1,"light material has a subtle tonal gradient");
                    Preferences.Current.DockBackgroundKind=3;window.RefreshGlassMaterial();
                    Require(window.Surface.Background==Brushes.Transparent&&window.Surface.BorderThickness.Left==0&&Native.BlurOpacity(window)==0&&window.Surface.Effect==null,"icons-only mode leaves the entire dock surface empty");
                    Require(label.Foreground==Paint.ControlWhite&&glyph.Color==Paint.ControlWhite&&label.Effect!=null&&label.Effect.IsFrozen&&Object.ReferenceEquals(label.Effect,glyph.Effect),"small foreground outlines are frozen, shared and independent of theme");
                    Preferences.Current.DockBackgroundKind=1;window.RefreshGlassMaterial();
                    Require(window.Surface.Background==DockAppearance.Background&&Native.BlurOpacity(window)==0&&label.Effect==null,"explicit solid mode remains available and removes icon-only outlines");
                    Preferences.Current.DockBackgroundKind=0;window.RefreshGlassMaterial();
                    Require(window.Surface.Background==LightPanel.DockGlass&&Native.BlurOpacity(window)>.99&&glyph.Effect==null,"returning from custom modes restores live glass without recreating the dock");
                    window.Close();window=null;
                }
            }finally{if(window!=null)window.Close();Preferences.Current.UIVersion=version;Preferences.Current.DockBackgroundKind=kind;Preferences.Current.Blur=blur;Preferences.Current.DockSolidBackground=solid;}
        }
    }
}
