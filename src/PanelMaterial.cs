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
        static readonly Brush[] protectedTints=new Brush[4];
        static readonly Brush protectedDark=Paint.Brush("#C4000000"),protectedLight=Paint.Brush("#DCFFFFFF");
        internal static bool ProtectContrast {get{return Preferences.Current.Blur&&!SystemParameters.HighContrast;}}
        internal static Brush ReadableCard(bool dark){return dark?protectedDark:protectedLight;}
        internal static Brush ReadableSurface(bool dark,bool modern,bool full=false)
        {
            var original=Surface(dark,modern);if(!ProtectContrast)return original;if(full)return ReadableCard(dark);int index=(dark?2:0)+(modern?1:0);if(protectedTints[index]!=null)return protectedTints[index];
            var source=(LinearGradientBrush)original;Color clear=source.GradientStops[1].Color,strong=clear;strong.A=dark?(byte)196:(byte)220;var brush=new LinearGradientBrush {StartPoint=new Point(0,0),EndPoint=new Point(0,1)};foreach(var stop in new[]{new GradientStop(strong,0),new GradientStop(strong,.10),new GradientStop(clear,.18),new GradientStop(clear,.82),new GradientStop(strong,.92),new GradientStop(strong,1)})brush.GradientStops.Add(stop);brush.Freeze();return protectedTints[index]=brush;
        }
        internal static void RefreshCards(DependencyObject root)
        {
            if(root is MediaPlayerView)return;var border=root as Border;var control=root as Control;Brush current=border!=null?border.Background:control!=null?control.Background:null;
            if(current==protectedDark||current==protectedLight||current==LightCard||current==Paint.Brush("#18FFFFFF")||Glass95.CardBrush(current)){var next=QuickPanel.Card;if(current!=next){if(border!=null)border.Background=next;else control.Background=next;}}
            foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)RefreshCards(child);}
        }
        internal static bool Light {get{return !Glass95.Dark;}}
        internal static readonly Brush LightText=Paint.Brush("#FF172336"),LightMuted=Paint.Brush("#FF46546A"),LightCard=Paint.Brush("#5994A1AF"),LightSolid=Paint.Brush("#FFE4E9ED");
        internal static Brush Surface(bool dark,bool modern)
        {
            int index=(dark?2:0)+(modern?1:0);var cached=tints[index];if(cached!=null)return cached;
            Color color=dark?(modern?Color.FromRgb(18,23,32):Colors.Black):modern?Color.FromRgb(245,248,253):Color.FromRgb(226,233,241);byte alpha=(byte)Math.Round((100-Transparency)*255/100.0);
            var brush=new LinearGradientBrush {StartPoint=new System.Windows.Point(0,0),EndPoint=new System.Windows.Point(0,1)};
            foreach(double offset in new[]{0,.48,1})brush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,color.R,color.G,color.B),offset));brush.Freeze();return tints[index]=brush;
        }
    }
}
