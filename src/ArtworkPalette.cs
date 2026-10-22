using System;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal sealed class ArtworkPalette
    {
        static readonly ConditionalWeakTable<BitmapSource,ArtworkPalette> cache=new ConditionalWeakTable<BitmapSource,ArtworkPalette>();
        static readonly ConditionalWeakTable<BitmapSource,Brush> playerSurfaces=new ConditionalWeakTable<BitmapSource,Brush>();
        internal readonly Color Dominant,Background;internal readonly Brush Glass,Solid,Ink;internal readonly double MinimumContrast;
        static readonly Color text=Color.FromRgb(244,248,245);const byte alpha=220;
        ArtworkPalette(Color dominant)
        {
            Dominant=dominant;Color tone=Shade(dominant,.45);for(int i=0;i<32&&Contrast(text,OnWhite(tone,alpha))<5.4;i++)tone=Shade(tone,.90);Background=tone;MinimumContrast=Contrast(text,OnWhite(tone,alpha));
            var glass=new LinearGradientBrush {StartPoint=new System.Windows.Point(0,0),EndPoint=new System.Windows.Point(0,1)};glass.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,tone.R,tone.G,tone.B),0));var lower=Shade(tone,.78);glass.GradientStops.Add(new GradientStop(Color.FromArgb(alpha,lower.R,lower.G,lower.B),1));glass.Freeze();Glass=glass;var solid=new SolidColorBrush(tone);solid.Freeze();Solid=solid;Ink=solid;
        }
        internal static readonly ArtworkPalette Neutral=new ArtworkPalette(Color.FromRgb(104,115,110));
        internal static ArtworkPalette Read(BitmapSource source){if(source==null)return Neutral;return cache.GetValue(source,Extract);}
        internal Brush PlayerSurface(BitmapSource source)
        {
            return source==null?Glass:playerSurfaces.GetValue(source,CreatePlayerSurface);
        }
        Brush CreatePlayerSurface(BitmapSource source)
        {
            // One frozen, bounded-size artwork surface per track; no live blur or per-tick bitmap work.
            double scale=Math.Min(1,640.0/Math.Max(source.PixelWidth,source.PixelHeight));BitmapSource image=source;
            if(scale<1){image=new TransformedBitmap(source,new ScaleTransform(scale,scale));image.Freeze();}
            var picture=new ImageBrush(image) {Stretch=Stretch.UniformToFill};picture.Freeze();
            var tint=new LinearGradientBrush {StartPoint=new System.Windows.Point(0,0),EndPoint=new System.Windows.Point(0,1)};
            var tone=Shade(Background,.2);tint.GradientStops.Add(new GradientStop(Color.FromArgb(176,tone.R,tone.G,tone.B),0));tint.GradientStops.Add(new GradientStop(Color.FromArgb(196,tone.R,tone.G,tone.B),1));tint.Freeze();
            var drawing=new DrawingGroup();var bounds=new System.Windows.Rect(0,0,400,188);using(var context=drawing.Open()){context.DrawRectangle(picture,null,bounds);context.DrawRectangle(tint,null,bounds);}drawing.Freeze();
            var brush=new DrawingBrush(drawing) {Stretch=Stretch.UniformToFill,Opacity=.94};brush.Freeze();return brush;
        }
        static ArtworkPalette Extract(BitmapSource image)
        {
            try {
                double factor=Math.Min(1,32.0/Math.Max(image.PixelWidth,image.PixelHeight));BitmapSource source=factor<1?new TransformedBitmap(image,new ScaleTransform(factor,factor)):(BitmapSource)image;var converted=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int stride=converted.PixelWidth*4;byte[] pixels=new byte[stride*converted.PixelHeight];converted.CopyPixels(pixels,stride,0);
                var weights=new int[512];var counts=new int[512];var red=new long[512];var green=new long[512];var blue=new long[512];int total=0,colorful=0;
                for(int i=0;i<pixels.Length;i+=4){int a=pixels[i+3];if(a<128)continue;int r=pixels[i+2],g=pixels[i+1],b=pixels[i],max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b));bool color=max-min>=28&&max>50&&min<225;int weight=color?2+(max-min)/64:1,key=(r/32)*64+(g/32)*8+b/32;weights[key]+=weight;counts[key]++;red[key]+=(long)r*weight;green[key]+=(long)g*weight;blue[key]+=(long)b*weight;total++;if(color)colorful++;}
                if(total==0)return Neutral;int best=-1;bool preferColor=colorful>=total*.12;
                for(int i=0;i<512;i++){if(weights[i]==0)continue;int r=(int)(red[i]/weights[i]),g=(int)(green[i]/weights[i]),b=(int)(blue[i]/weights[i]);if(preferColor&&Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<28)continue;if(best<0||weights[i]>weights[best])best=i;}
                if(best<0)return Neutral;return new ArtworkPalette(Color.FromRgb((byte)(red[best]/weights[best]),(byte)(green[best]/weights[best]),(byte)(blue[best]/weights[best])));
            }catch(Exception error){AppLog.Once("artwork-palette",error.Message);return Neutral;}
        }
        static Color Shade(Color c,double factor){return Color.FromRgb((byte)Math.Round(c.R*factor),(byte)Math.Round(c.G*factor),(byte)Math.Round(c.B*factor));}
        static Color OnWhite(Color c,byte opacity){double a=opacity/255.0*.955;return Color.FromRgb((byte)Math.Round(c.R*a+255*(1-a)),(byte)Math.Round(c.G*a+255*(1-a)),(byte)Math.Round(c.B*a+255*(1-a)));}
        static double Channel(byte c){double x=c/255.0;return x<=.04045?x/12.92:Math.Pow((x+.055)/1.055,2.4);}
        static double Luminance(Color c){return .2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);}
        static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        internal static BitmapSource Sample(Color color){var pixels=new byte[16*16*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=color.A;}var image=BitmapSource.Create(16,16,96,96,PixelFormats.Bgra32,null,pixels,64);image.Freeze();return image;}
        internal static void Check(){foreach(var color in new[]{Colors.White,Colors.Black,Colors.Yellow,Colors.Red,Colors.Blue,Colors.Transparent}){var palette=Read(Sample(color));if(palette.MinimumContrast<5.4)throw new Exception("Artwork palette has insufficient title contrast");}var redPalette=Read(Sample(Colors.Red));if(redPalette.Dominant.R<redPalette.Dominant.B+80||redPalette.Dominant.R<redPalette.Dominant.G+80)throw new Exception("Artwork palette lost the dominant color");}
    }
}
