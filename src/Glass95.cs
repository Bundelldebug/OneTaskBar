using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneTaskBar
{
    // The blur is a live Windows Composition host backdrop, not a captured bitmap.
    // These frozen tints and edge lights provide the 9.5 material above that backdrop.
    internal static class Glass95
    {
        sealed class Palette
        {
            internal readonly Brush Surface,Card,Solid,Text,Muted,Rail,Toggle,Rim;
            internal Palette(bool dark)
            {
                Surface=PanelMaterial.PanelSurface(dark,true);
                Card=Film(true);
                Solid=dark?PanelMaterial.DarkSolid:Paint.Brush("#FF242832");Text=dark?PanelMaterial.DarkText:Paint.Brush("#FFF5F7FC");Muted=dark?PanelMaterial.DarkMuted:Paint.Brush("#FFD1D9E7");
                Rail=Paint.Brush("#626C7A91");Toggle=Paint.Brush("#28FFFFFF");Rim=Edge(true,true);
            }
        }
        static readonly Palette light=new Palette(false),dark=new Palette(true);
        static bool? fixtureDark;
        internal static bool Enabled {get{return ReleaseFeatures.GlassUI&&Preferences.Current.UIVersion>=95;}}
        internal static bool Dark {get{return fixtureDark??AppTheme.Dark;}}
        internal static bool DockDark {get{return fixtureDark??AppTheme.DockDark;}}
        static Palette Current {get{return Dark?dark:light;}}
        internal static bool CardBrush(Brush brush){return brush!=null&&(brush==light.Card||brush==dark.Card);}
        internal static bool Transparent {get{return Preferences.Current.Blur&&!SystemParameters.HighContrast;}}
        internal static Brush Surface {get{return PanelMaterial.PanelSurface(Dark,true);}}
        internal static Brush Card {get{return Transparent?Current.Card:PanelMaterial.Card(Dark);}}
        internal static Brush Solid {get{return SystemParameters.HighContrast?SystemColors.WindowBrush:Current.Solid;}}
        internal static Brush Text {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:Current.Text;}}
        internal static Brush Muted {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:Current.Muted;}}
        internal static Brush Rail {get{return Current.Rail;}}
        internal static Brush Toggle {get{return Current.Toggle;}}
        internal static Brush Rim {get{return Transparent?Current.Rim:Brushes.Transparent;}}
        internal static readonly Brush Accent=Paint.Brush("#FF3478FD"),Selected=Paint.ControlWhite;
        static Brush Film(bool dark)
        {
            var film=new LinearGradientBrush {StartPoint=new Point(.08,0),EndPoint=new Point(.4,1)};
            film.GradientStops.Add(new GradientStop(Color.FromArgb(13,255,255,255),0));
            film.GradientStops.Add(new GradientStop(Color.FromArgb(3,255,255,255),.23));
            film.GradientStops.Add(new GradientStop(Color.FromArgb(12,6,14,24),.58));
            film.GradientStops.Add(new GradientStop(Color.FromArgb(32,5,12,20),1));
            film.Freeze();return film;
        }
        internal static Brush ToggleInk(bool value){return value?Paint.OnLight:Paint.Text;}
        internal static Brush DockForeground {get{return DockAppearance.Custom?DockAppearance.Foreground:DockDark?Paint.ControlWhite:Paint.OnLight;}}
        internal static LinearGradientBrush Edge(bool isDark,bool freeze=false)
        {
            var edge=new LinearGradientBrush {StartPoint=new Point(.12,0),EndPoint=new Point(.84,1)};
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(isDark?(byte)78:(byte)105,255,255,255),0));
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(isDark?(byte)24:(byte)45,221,232,249),.45));
            edge.GradientStops.Add(new GradientStop(Color.FromArgb(isDark?(byte)28:(byte)58,119,140,170),1));if(freeze)edge.Freeze();return edge;
        }
        internal static void DockInk(DependencyObject root)
        {
            var text=root as TextBlock;if(text!=null){text.Foreground=DockForeground;text.Effect=DockAppearance.Readability;}
            var glyph=root as Glyph;if(glyph!=null){glyph.Effect=DockAppearance.Readability;if(glyph.Kind!="windows-start")glyph.Color=DockForeground;glyph.InvalidateVisual();}
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)DockInk(node);}
        }
        internal static void MediaInk(DependencyObject root)
        {
            var text=root as TextBlock;if(text!=null)text.Foreground=Paint.ControlWhite;
            var glyph=root as Glyph;if(glyph!=null)glyph.Color=Paint.ControlWhite;
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)MediaInk(node);}
        }
        static bool Same(Brush a,Brush b){if(Object.ReferenceEquals(a,b))return true;var x=a as SolidColorBrush;var y=b as SolidColorBrush;return x!=null&&y!=null&&x.Color==y.Color;}
        static Brush Recolor(Brush brush)
        {
            if(brush==null)return null;
            foreach(var old in new[]{light,dark}){
                if(Same(brush,old.Text))return Text;if(Same(brush,old.Muted))return Muted;if(Same(brush,old.Surface))return Surface;if(Same(brush,old.Card))return Card;if(Same(brush,old.Solid))return Solid;if(Same(brush,old.Toggle))return Toggle;if(Same(brush,old.Rim))return Rim;
            }return brush;
        }
        internal static void RefreshColors(DependencyObject root)
        {
            if(!Enabled||root is MediaPlayerView)return;
            var text=root as TextBlock;if(text!=null)text.Foreground=Recolor(text.Foreground);
            var control=root as Control;if(control!=null){control.Foreground=Recolor(control.Foreground);control.Background=Recolor(control.Background);control.BorderBrush=Recolor(control.BorderBrush);}
            var border=root as Border;if(border!=null){border.Background=Recolor(border.Background);border.BorderBrush=Recolor(border.BorderBrush);}
            var glyph=root as Glyph;if(glyph!=null){glyph.Color=Recolor(glyph.Color);glyph.InvalidateVisual();}
            var slider=root as PillSlider;if(slider!=null)slider.InvalidateVisual();
            foreach(object child in LogicalTreeHelper.GetChildren(root)){var node=child as DependencyObject;if(node!=null)RefreshColors(node);}
        }
        internal static IDisposable WithDarkForFixture(bool value)
        {
            if(!Program.Inspectable)throw new InvalidOperationException("Glass palette fixture requires inspectable mode");return new PaletteFixture(value);
        }
        sealed class PaletteFixture : IDisposable
        {
            readonly bool? previous;bool disposed;internal PaletteFixture(bool value){previous=fixtureDark;fixtureDark=value;}
            public void Dispose(){if(disposed)return;disposed=true;fixtureDark=previous;}
        }
    }
}
