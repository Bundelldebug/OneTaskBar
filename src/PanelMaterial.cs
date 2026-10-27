using System;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
namespace OneTaskBar
{
    internal static class PanelMaterial
    {
        internal const int Transparency=70;
        static readonly Brush[] tints=new Brush[4];
        static readonly Brush[] panels=new Brush[4];
        internal const int LightPanelOpacity=30,DarkPanelOpacity=88;
        internal static void RefreshCards(DependencyObject root)
        {
            if(root is MediaPlayerView)return;var border=root as Border;var control=root as Control;Brush current=border!=null?border.Background:control!=null?control.Background:null;
            if(current==LightCard||current==DarkCard||Glass95.CardBrush(current)){var next=QuickPanel.Card;if(current!=next){if(border!=null)border.Background=next;else control.Background=next;}}
            foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)RefreshCards(child);}
        }
        internal static bool Light {get{return !Glass95.Dark;}}
        internal static readonly Brush LightText=Paint.Brush("#FF172336"),LightMuted=Paint.Brush("#FF46546A"),LightCard=Paint.Brush("#2694A1AF"),LightSolid=Paint.Brush("#FFE4E9ED");
        internal static readonly Brush DarkText=Paint.Brush("#FFF5F7FA"),DarkMuted=Paint.Brush("#FFCDD4DE"),DarkCard=Paint.Brush("#18FFFFFF"),DarkSolid=Paint.Brush("#FF0D1014");
        static readonly Brush classicText=Paint.Brush("#FFFFFFFF"),classicMuted=Paint.Brush("#FFDBE3DF"),classicGlassText=Paint.Brush("#FFF5F7FC"),classicGlassMuted=Paint.Brush("#FFD1D9E7"),classicSolid=Paint.Brush("#FF29302D"),classicGlassSolid=Paint.Brush("#FF242832");
        static readonly Brush field=Paint.Brush("#20FFFFFF"),stroke=Paint.Brush("#507F8B9B"),focus=Paint.Brush("#FF8DB7FF");
        internal static Brush Text(bool dark){return SystemParameters.HighContrast?SystemColors.WindowTextBrush:dark?DarkText:Glass95.Enabled?classicGlassText:classicText;}
        internal static Brush Muted(bool dark){return SystemParameters.HighContrast?SystemColors.WindowTextBrush:dark?DarkMuted:Glass95.Enabled?classicGlassMuted:classicMuted;}
        internal static Brush Solid(bool dark){return SystemParameters.HighContrast?SystemColors.WindowBrush:dark?DarkSolid:Glass95.Enabled?classicGlassSolid:classicSolid;}
        internal static Brush Card(bool dark){return SystemParameters.HighContrast?SystemColors.WindowBrush:DarkCard;}
        internal static Brush Field {get{return SystemParameters.HighContrast?SystemColors.WindowBrush:field;}}
        internal static Brush FieldStroke {get{return SystemParameters.HighContrast?SystemColors.WindowTextBrush:stroke;}}
        internal static Brush Focus {get{return SystemParameters.HighContrast?SystemColors.HighlightBrush:focus;}}
        internal static bool ReadablePanel(Window window){return window is DesktopOverview||window is QuickWindow||window is NotificationWindow||window is DetailWindow||window is CalendarWindow||window is StartWindow||window is SearchWindow||window is DrawerWindow;}
        internal static bool NotificationPopup(Window window){return window is NotificationBanner||window is NotificationConnectWindow||window is BudsConnectionPopup||window is WindowSwitcherView;}
        internal static Brush PopupSurface(bool dark)
        {
            return PanelSurface(dark,Glass95.Enabled);
        }
        internal static Brush PanelSurface(bool dark,bool modern)
        {
            if(SystemParameters.HighContrast)return SystemColors.WindowBrush;
            // Light mode intentionally retains the previous dark-mode material verbatim.
            if(!dark)return Surface(true,modern);
            int index=modern?1:0;var cached=panels[index];if(cached!=null)return cached;var color=((SolidColorBrush)DarkSolid).Color;color.A=(byte)Math.Round(DarkPanelOpacity*255/100.0);var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};foreach(double offset in new[]{0,.48,1})brush.GradientStops.Add(new GradientStop(color,offset));brush.Freeze();return panels[index]=brush;
        }
        internal static Brush Surface(bool dark,bool modern)
        {
            int index=(dark?2:0)+(modern?1:0);var cached=tints[index];if(cached!=null)return cached;
            Color color=dark?(modern?Color.FromRgb(18,23,32):Colors.Black):modern?Color.FromRgb(245,248,253):Color.FromRgb(226,233,241);byte alpha=(byte)Math.Round((100-Transparency)*255/100.0);
            var brush=new LinearGradientBrush {StartPoint=new System.Windows.Point(0,0),EndPoint=new System.Windows.Point(0,1)};
            foreach(double offset in new[]{0,.48,1})brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,color.R,color.G,color.B),offset));brush.Freeze();return tints[index]=brush;
        }
    }
}
