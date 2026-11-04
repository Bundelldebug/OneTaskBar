using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // A desktop widget is a child window, so Windows offers it no backdrop blur. What lies
    // behind it is the wallpaper, so one blurred copy of that area is rendered and reused.
    // It redraws only when the widget moves or the options or wallpaper change.
    internal static class WidgetMaterial
    {
        const double Detail=.5;
        internal const int ReadableOpacity=60;
        internal static bool Plain {get{return Preferences.Current.WidgetOpacity>=100&&Preferences.Current.WidgetBlur<=0;}}
        internal static Brush Blurred(Brush wallpaper,Rect screen,Rect widget,double radius)
        {
            if(wallpaper==null||radius<=0||widget.Width<1||widget.Height<1)return null;
            int margin=(int)Math.Ceiling(radius*Detail*2);
            int width=Math.Max(1,(int)Math.Ceiling(widget.Width*Detail)),height=Math.Max(1,(int)Math.Ceiling(widget.Height*Detail));
            var visual=new DrawingVisual {Effect=new BlurEffect {Radius=radius*Detail,KernelType=KernelType.Gaussian,RenderingBias=RenderingBias.Performance}};
            using(var draw=visual.RenderOpen())
            {
                draw.DrawRectangle(wallpaper,null,new Rect((screen.X-widget.X)*Detail+margin,(screen.Y-widget.Y)*Detail+margin,screen.Width*Detail,screen.Height*Detail));
            }
            var bitmap=new RenderTargetBitmap(width+margin*2,height+margin*2,96,96,PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            var cropped=new CroppedBitmap(bitmap,new Int32Rect(margin,margin,width,height));
            cropped.Freeze();
            var brush=new ImageBrush(cropped) {Stretch=Stretch.Fill};
            brush.Freeze();
            return brush;
        }
        internal static Brush Surface(TileScheme scheme,int opacity)
        {
            var solid=scheme.Surface as SolidColorBrush;
            var color=solid!=null?solid.Color:Colors.White;
            var brush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Math.Max(0,Math.Min(100,opacity))/100.0),color.R,color.G,color.B));
            brush.Freeze();
            return brush;
        }
    }
}
