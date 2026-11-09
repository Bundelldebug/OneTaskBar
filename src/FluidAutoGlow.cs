using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // The automatic glow color of the Fluid UI: the main color of the desktop wallpaper, lifted to a clear light so it
    // sits well behind the glass. A grey or missing wallpaper falls back to the default orange.
    internal static class FluidAutoGlow
    {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]
        static extern bool SystemParametersInfo(int action,int length,StringBuilder result,int flags);
        static readonly Color Fallback=Color.FromRgb(0xFF,0x8A,0x3D);
        static Color current=Fallback;
        static string key="";
        static int checkedAt;
        static bool checkedOnce;

        // Looked up at most every few seconds; the picture is only decoded again when the wallpaper file changed.
        internal static Color Color
        {
            get
            {
                int now=Environment.TickCount;
                if(checkedOnce&&unchecked(now-checkedAt)<5000)return current;
                checkedOnce=true;
                checkedAt=now;
                try
                {
                    Color background;
                    var screen=System.Windows.Forms.Screen.PrimaryScreen;
                    string path=DesktopWallpaper.CurrentFile(screen==null?new System.Drawing.Rectangle(0,0,1,1):screen.Bounds,out background);
                    // Wallpaper Engine and similar tools keep their picture elsewhere.
                    string engine=screen==null?null:WallpaperEngineSource.Current(screen.Bounds);
                    if(engine!=null)path=engine;
                    if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith("\\\\",StringComparison.Ordinal)||!File.Exists(path))
                    {
                        // A plain color desktop glows in that color.
                        string plain="color|"+background.R+","+background.G+","+background.B;
                        if(plain==key)return current;
                        key=plain;
                        current=Lift(background,false);
                        return current;
                    }
                    string stamp=path+"|"+File.GetLastWriteTimeUtc(path).Ticks;
                    if(stamp==key)return current;
                    key=stamp;
                    current=Extract(path);
                }
                catch(Exception error)
                {
                    AppLog.Once("fluid-auto-glow",error.GetType().Name);
                    current=Fallback;
                }
                return current;
            }
        }

        static Color Extract(string path)
        {
            if(new FileInfo(path).Length>67108864)return Fallback;
            var image=new BitmapImage();
            using(var stream=File.OpenRead(path))
            {
                image.BeginInit();
                image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=64;
                image.StreamSource=stream;
                image.EndInit();
                image.Freeze();
            }
            return Lift(ArtworkPalette.Read(image).Dominant,Object.ReferenceEquals(ArtworkPalette.Read(image),ArtworkPalette.Neutral));
        }

        // Keeps the hue, and makes it vivid and bright enough to glow; a colorless picture gets the default orange.
        internal static Color Lift(Color color,bool neutral)
        {
            double r=color.R/255.0,g=color.G/255.0,b=color.B/255.0;
            double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
            if(neutral||delta<.08)return Fallback;
            double hue;
            if(max==r)hue=((g-b)/delta+6)%6;
            else if(max==g)hue=(b-r)/delta+2;
            else hue=(r-g)/delta+4;
            return FromHsv(hue*60,Math.Max(.55,Math.Min(1,delta/max)),Math.Max(.92,max));
        }

        static Color FromHsv(double hue,double saturation,double value)
        {
            double c=value*saturation,x=c*(1-Math.Abs(hue/60%2-1)),m=value-c,r,g,b;
            if(hue<60){r=c;g=x;b=0;}
            else if(hue<120){r=x;g=c;b=0;}
            else if(hue<180){r=0;g=c;b=x;}
            else if(hue<240){r=0;g=x;b=c;}
            else if(hue<300){r=x;g=0;b=c;}
            else{r=c;g=0;b=x;}
            return Color.FromRgb((byte)Math.Round((r+m)*255),(byte)Math.Round((g+m)*255),(byte)Math.Round((b+m)*255));
        }
    }
}
