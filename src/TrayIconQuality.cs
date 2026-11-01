using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // A saved image is useful only while it still matches the live tray state.
    // Compare the silhouette and interior colors before retaining its extra detail.
    internal sealed class TrayIconQuality
    {
        const int Samples=24;
        readonly BitmapSource original;
        readonly Pixels reference;
        readonly bool monochrome;

        internal TrayIconQuality(ImageSource source)
        {
            original=source as BitmapSource;
            if(original==null||original.PixelWidth>256||original.PixelHeight>256)return;
            reference=new Pixels(original);
            monochrome=TrayIconVisual.IsMonochrome(original);
        }

        internal BitmapSource Choose(BitmapSource live,Color matte)
        {
            if(reference==null||Math.Max(original.PixelWidth,original.PixelHeight)<=Math.Max(live.PixelWidth,live.PixelHeight))return live;
            var current=new Pixels(live);
            double contrast=0;
            if(monochrome)
            {
                foreach(var value in current.Values)
                    contrast=Math.Max(contrast,Distance(value,matte));
                if(contrast<.08)return live;
                foreach(var value in current.Values)
                {
                    if(value.A>.8&&Distance(value,matte)>contrast*.65&&
                        Math.Max(value.R,Math.Max(value.G,value.B))-Math.Min(value.R,Math.Min(value.G,value.B))>.12)return live;
                }
            }

            double best=Double.MaxValue;
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {
                double error=0;
                int wrongColor=0,common=0;
                for(int y=0;y<Samples;y++)for(int x=0;x<Samples;x++)
                {
                    var a=reference.Sample(x,y);
                    var b=current.Sample(x+dx,y+dy);
                    if(monochrome)
                    {
                        error+=Math.Abs(a.A-b.A*Math.Min(1,Distance(b,matte)/contrast));
                    }
                    else
                    {
                        double red=Math.Abs(Composite(a.R,a.A,matte.R)-Composite(b.R,b.A,matte.R));
                        double green=Math.Abs(Composite(a.G,a.A,matte.G)-Composite(b.G,b.A,matte.G));
                        double blue=Math.Abs(Composite(a.B,a.A,matte.B)-Composite(b.B,b.A,matte.B));
                        error+=(red+green+blue)/3;
                        if(a.A>.85&&b.A>.85)
                        {
                            common++;
                            if(Math.Max(red,Math.Max(green,blue))>.35)wrongColor++;
                        }
                    }
                }
                if(wrongColor>Math.Max(2,common*.015))continue;
                best=Math.Min(best,error/(Samples*Samples));
            }
            return best<.11?original:live;
        }

        static double Composite(double color,double alpha,byte matte){return color*alpha+matte/255.0*(1-alpha);}
        static double Distance(Pixel pixel,Color matte)
        {
            return Math.Max(Math.Abs(pixel.R-matte.R/255.0),Math.Max(Math.Abs(pixel.G-matte.G/255.0),Math.Abs(pixel.B-matte.B/255.0)));
        }

        struct Pixel
        {
            internal double R,G,B,A;
            internal static Pixel Mix(Pixel a,Pixel b,double t)
            {
                return new Pixel {R=a.R+(b.R-a.R)*t,G=a.G+(b.G-a.G)*t,B=a.B+(b.B-a.B)*t,A=a.A+(b.A-a.A)*t};
            }
        }

        sealed class Pixels
        {
            internal readonly Pixel[] Values;
            readonly int width,height,left,top,right,bottom;
            internal Pixels(BitmapSource source)
            {
                var image=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
                width=image.PixelWidth;height=image.PixelHeight;
                var bytes=new byte[width*height*4];image.CopyPixels(bytes,width*4,0);
                Values=new Pixel[width*height];
                int l=width,t=height,r=-1,b=-1;
                for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                {
                    int i=y*width+x,p=i*4;
                    Values[i]=new Pixel {R=bytes[p+2]/255.0,G=bytes[p+1]/255.0,B=bytes[p]/255.0,A=bytes[p+3]/255.0};
                    if(bytes[p+3]>12){l=Math.Min(l,x);t=Math.Min(t,y);r=Math.Max(r,x);b=Math.Max(b,y);}
                }
                left=r<l?0:l;top=b<t?0:t;right=r<l?width-1:r;bottom=b<t?height-1:b;
            }
            internal Pixel Sample(int x,int y)
            {
                if(x<0||y<0||x>=Samples||y>=Samples)return new Pixel();
                double px=left+(x+.5)*(right-left+1)/Samples-.5;
                double py=top+(y+.5)*(bottom-top+1)/Samples-.5;
                px=Math.Max(0,Math.Min(width-1,px));py=Math.Max(0,Math.Min(height-1,py));
                int x0=(int)px,y0=(int)py,x1=Math.Min(width-1,x0+1),y1=Math.Min(height-1,y0+1);
                return Pixel.Mix(Pixel.Mix(Values[y0*width+x0],Values[y0*width+x1],px-x0),
                    Pixel.Mix(Values[y1*width+x0],Values[y1*width+x1],px-x0),py-y0);
            }
        }
    }
}
