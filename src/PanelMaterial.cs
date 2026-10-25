using System;
using System.Windows.Media;
namespace OneTaskBar
{
    internal static class PanelMaterial
    {
        static readonly Brush[,] tints=new Brush[4,101];
        internal static bool Light {get{return !Glass95.Dark;}}
        internal static readonly Brush LightText=Paint.Brush("#FF172336"),LightMuted=Paint.Brush("#FF46546A"),LightCard=Paint.Brush("#5994A1AF"),LightSolid=Paint.Brush("#FFE4E9ED");
        internal static Brush Surface(bool dark,bool modern)
        {
            int transparency=Math.Max(0,Math.Min(100,Preferences.Current.UITransparency)),index=(dark?2:0)+(modern?1:0);var cached=tints[index,transparency];if(cached!=null)return cached;
            Color color=dark?(modern?Color.FromRgb(18,23,32):Colors.Black):modern?Color.FromRgb(245,248,253):Color.FromRgb(226,233,241);double midpoint=modern?.54:.60,alpha=transparency<=50?1-(1-midpoint)*transparency/50.0:midpoint-(midpoint-.06)*(transparency-50)/50.0;
            var brush=new LinearGradientBrush {StartPoint=new System.Windows.Point(0,0),EndPoint=new System.Windows.Point(0,1)};
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(Math.Min(1,alpha+(modern?.045:0))*255),color.R,color.G,color.B),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha*(modern?255:247)),color.R,color.G,color.B),.48));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha*255),color.R,color.G,color.B),1));brush.Freeze();return tints[index,transparency]=brush;
        }
    }
}
