using System.Windows.Media;
namespace OneTaskBar
{
    internal static class NotificationStyle
    {
        static readonly Brush lightCard=Paint.Brush("#BDF7FFF9");
        static readonly Brush lightInk=Paint.Brush("#FF14201C");
        static readonly Brush lightMuted=Paint.Brush("#FF3D4B45");
        static readonly Brush darkCard=Paint.Brush("#C92A3331");
        internal static Brush Surface {get{return PanelMaterial.PanelSurface(Glass95.Dark,Glass95.Enabled);}}
        internal static Brush Solid {get{return PanelMaterial.Solid(Glass95.Dark);}}
        internal static Brush Card {get{return Glass95.Dark?darkCard:lightCard;}}
        internal static Brush Text {get{return Glass95.Dark?PanelMaterial.DarkText:lightInk;}}
        internal static Brush Muted {get{return Glass95.Dark?PanelMaterial.DarkMuted:lightMuted;}}
    }
}
