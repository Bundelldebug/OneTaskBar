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

        static FluidAutoGlow()
        {
            // A new picture (also a slideshow or Spotlight change) is looked at again right away, not after the polling pause.
            try
            {
                Microsoft.Win32.SystemEvents.UserPreferenceChanged+=(s,e)=>
                {
                    if(e.Category==Microsoft.Win32.UserPreferenceCategory.Desktop){checkedOnce=false;}
                };
            }
            catch(Exception error){AppLog.Once("fluid-auto-glow-event",error.GetType().Name);}
        }

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
                        current=FromBackground(background);
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

        // A colorless wallpaper glows a soft cool white instead of the default orange, which would clash with it.
        static readonly Color Silver=Color.FromRgb(0xE4,0xEA,0xF4);

        static Color FromBackground(Color color)
        {
            double max=Math.Max(color.R,Math.Max(color.G,color.B))/255.0,min=Math.Min(color.R,Math.Min(color.G,color.B))/255.0;
            return max-min<.08?Silver:Lift(color,false);
        }

        // The wallpaper's main color: pixels are weighted by how vivid and bright they are and grouped by hue, so a large dark
        // or grey area does not outvote the colored part, and the strongest hue (with its neighbours) is averaged.
        static Color Extract(string path)
        {
            if(new FileInfo(path).Length>67108864)return Fallback;
            var image=new BitmapImage();
            using(var stream=File.OpenRead(path))
            {
                image.BeginInit();
                image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=96;
                image.StreamSource=stream;
                image.EndInit();
                image.Freeze();
            }
            var converted=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
            int width=converted.PixelWidth,height=converted.PixelHeight,stride=width*4;
            var pixels=new byte[stride*height];
            converted.CopyPixels(pixels,stride,0);
            const int Buckets=36;
            var weight=new double[Buckets];
            var red=new double[Buckets];
            var green=new double[Buckets];
            var blue=new double[Buckets];
            int counted=0;
            for(int i=0;i+3<pixels.Length;i+=4)
            {
                if(pixels[i+3]<128)continue;
                counted++;
                double b=pixels[i]/255.0,g=pixels[i+1]/255.0,r=pixels[i+2]/255.0;
                double max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b)),delta=max-min;
                if(max<.12||delta<.06)continue;
                double saturation=delta/max;
                if(saturation<.1)continue;
                double hue;
                if(max==r)hue=((g-b)/delta+6)%6;
                else if(max==g)hue=(b-r)/delta+2;
                else hue=(r-g)/delta+4;
                int bucket=Math.Min(Buckets-1,(int)(hue*60/10));
                double w=saturation*Math.Sqrt(max);
                weight[bucket]+=w;red[bucket]+=r*w;green[bucket]+=g*w;blue[bucket]+=b*w;
            }
            if(counted==0)return Fallback;
            int best=0;double bestWeight=-1,total=0;
            for(int n=0;n<Buckets;n++)
            {
                total+=weight[n];
                double sum=weight[n]+weight[(n+1)%Buckets]+weight[(n+Buckets-1)%Buckets];
                if(sum>bestWeight){bestWeight=sum;best=n;}
            }
            // Hardly any colored pixel: a grey, black or white picture.
            if(bestWeight<counted*.015)return Silver;
            double wr=0,wg=0,wb=0,ww=0;
            for(int d=-1;d<=1;d++)
            {
                int n=(best+d+Buckets)%Buckets;
                wr+=red[n];wg+=green[n];wb+=blue[n];ww+=weight[n];
            }
            if(ww<=0)return Silver;
            var main=Color.FromRgb((byte)Math.Round(wr/ww*255),(byte)Math.Round(wg/ww*255),(byte)Math.Round(wb/ww*255));
            return FromBackground(main);
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
