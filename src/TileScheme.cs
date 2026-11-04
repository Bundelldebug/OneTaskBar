using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Colors for one set of tiles. Desktop widgets use a plain card that follows the
    // Windows theme; the collapsed media screen takes its ink from the wallpaper.
    internal sealed class TileScheme
    {
        internal Brush Ink,Muted,Track,Fill,Surface;
        internal bool Monochrome;
        internal string Key;
        static readonly Brush Good=Paint.Brush("#FF5FBF6B"),Low=Paint.Brush("#FFE25C5C");
        internal static readonly TileScheme Light=Plain("light","#FF111114","#FF6B6B73","#FFE6E6EA","#FFFBFBFD");
        internal static readonly TileScheme Dark=Plain("dark","#FFF5F5F7","#FFA8A8B0","#FF3A3C42","#FF26282C");
        internal static readonly TileScheme Unset=Tinted(Colors.Black,false);
        // Set while a tile is being constructed, on the UI thread.
        internal static TileScheme Creating=Light;
        static TileScheme Plain(string key,string ink,string muted,string track,string surface)
        {
            return new TileScheme {Key=key,Ink=Paint.Brush(ink),Muted=Paint.Brush(muted),Track=Paint.Brush(track),Fill=Paint.Brush(track),Surface=Paint.Brush(surface)};
        }
        internal static TileScheme Desktop(){return AppTheme.Read().Dark?Dark:Light;}
        internal Brush Level(int percent)
        {
            if(Monochrome)return Ink;
            return percent<=20?Low:Good;
        }
        static Brush Solid(Color color,byte alpha)
        {
            var brush=new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));
            brush.Freeze();
            return brush;
        }
        static Color FromHsl(double h,double s,double l)
        {
            double c=(1-Math.Abs(2*l-1))*s,x=c*(1-Math.Abs((h/60)%2-1)),m=l-c/2;
            double r=h<60?c:h<120?x:h<240?0:h<300?x:c;
            double g=h<60?x:h<180?c:h<240?x:0;
            double b=h<120?0:h<180?x:h<300?c:x;
            return Color.FromRgb((byte)((r+m)*255),(byte)((g+m)*255),(byte)((b+m)*255));
        }
        internal static double Luminance(Color color){return (color.R*.2126+color.G*.7152+color.B*.0722)/255;}
        // On a bright wallpaper the ink is a deep shade of its own color; on a dark one, a pale shade.
        internal static TileScheme Tinted(Color average,bool bright)
        {
            double r=average.R/255.0,g=average.G/255.0,b=average.B/255.0;
            double high=Math.Max(r,Math.Max(g,b)),low=Math.Min(r,Math.Min(g,b)),range=high-low,hue=0;
            if(range>.001)hue=high==r?60*(((g-b)/range+6)%6):high==g?60*((b-r)/range+2):60*((r-g)/range+4);
            double light=(high+low)/2,saturation=range<.001?0:range/(1-Math.Abs(2*light-1));
            Color ink=bright?FromHsl(hue,Math.Min(1,saturation*.7+.45),.2):FromHsl(hue,Math.Min(.35,saturation),.95);
            return new TileScheme
            {
                Key=ink.ToString(),
                Monochrome=true,
                Ink=Solid(ink,255),
                Muted=Solid(ink,190),
                Track=Solid(ink,52),
                Fill=Solid(ink,96),
                Surface=bright?Solid(FromHsl(hue,Math.Min(.5,saturation),Math.Max(.35,light-.12)),120):Solid(Colors.White,34)
            };
        }
        internal static TileScheme FromWallpaper(Brush wallpaper)
        {
            if(wallpaper==null)return Unset;
            var visual=new DrawingVisual();
            using(var draw=visual.RenderOpen())draw.DrawRectangle(wallpaper,null,new Rect(0,0,16,16));
            var bitmap=new RenderTargetBitmap(16,16,96,96,PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            byte[] pixels=AppIconShape.Pixels(bitmap);
            double r=0,g=0,b=0;
            for(int i=0;i<pixels.Length;i+=4)
            {
                b+=pixels[i];
                g+=pixels[i+1];
                r+=pixels[i+2];
            }
            int count=pixels.Length/4;
            var average=Color.FromRgb((byte)(r/count),(byte)(g/count),(byte)(b/count));
            return Tinted(average,Luminance(average)>.5);
        }
    }
}
