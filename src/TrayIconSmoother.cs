using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Tray icons arrive as 16 to 32 pixel pictures with stair-stepped edges. Showing them larger only softens the
    // steps; it does not remove them. This redraws a small icon on a finer grid: a smooth bicubic enlargement, then
    // the edge of the shape is pulled back to a clean contour, so the icon reads like a vector at any display scale.
    // The overflow window redraws with fresh bitmaps that usually hold the same picture, so results are shared by
    // content and the enlargement runs once per distinct icon.
    internal static class TrayIconSmoother
    {
        const int Target=128;
        static readonly ConditionalWeakTable<BitmapSource,BitmapSource> cache=new ConditionalWeakTable<BitmapSource,BitmapSource>();
        static readonly Dictionary<ulong,BitmapSource> byContent=new Dictionary<ulong,BitmapSource>();
        internal static int Builds {get;private set;}
        internal static BitmapSource Smooth(BitmapSource source)
        {
            if(source==null)return null;
            int width=source.PixelWidth,height=source.PixelHeight;
            if(Math.Max(width,height)>=96||Math.Min(width,height)<8)return source;
            BitmapSource done;
            if(cache.TryGetValue(source,out done))return done;
            var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
            int w=converted.PixelWidth,h=converted.PixelHeight,stride=w*4;
            var pixels=new byte[stride*h];
            converted.CopyPixels(pixels,stride,0);
            ulong hash=1469598103934665603UL^(ulong)(w*65537+h);
            for(int i=0;i<pixels.Length;i++)hash=(hash^pixels[i])*1099511628211UL;
            if(!byContent.TryGetValue(hash,out done))
            {
                done=Build(w,h,pixels);
                Builds++;
                if(byContent.Count>192)byContent.Clear();
                byContent[hash]=done;
            }
            cache.Add(source,done);
            return done;
        }
        static BitmapSource Build(int w,int h,byte[] pixels)
        {
            int stride=w*4;
            int factor=Math.Max(2,(int)Math.Ceiling(Target/(double)Math.Max(w,h)));
            int outW=w*factor,outH=h*factor;
            // The four bicubic weights depend only on the position inside a source pixel, so they repeat.
            var phases=new double[factor][];
            for(int k=0;k<factor;k++)
            {
                phases[k]=new double[4];
                Weights(((k+.5)/factor-.5)-Math.Floor((k+.5)/factor-.5),phases[k]);
            }
            var result=new byte[outW*outH*4];
            for(int y=0;y<outH;y++)
            {
                int y0=(int)Math.Floor((y+.5)/factor-.5);
                var wy=phases[y%factor];
                for(int x=0;x<outW;x++)
                {
                    int x0=(int)Math.Floor((x+.5)/factor-.5);
                    var wx=phases[x%factor];
                    double b=0,g=0,r=0,a=0;
                    for(int j=0;j<4;j++)
                    {
                        int row=Math.Max(0,Math.Min(h-1,y0-1+j))*stride;
                        double rowWeight=wy[j];
                        for(int i=0;i<4;i++)
                        {
                            int offset=row+Math.Max(0,Math.Min(w-1,x0-1+i))*4;
                            double weight=wx[i]*rowWeight;
                            b+=pixels[offset]*weight;g+=pixels[offset+1]*weight;r+=pixels[offset+2]*weight;a+=pixels[offset+3]*weight;
                        }
                    }
                    double alpha=Math.Max(0,Math.Min(255,a))/255.0;
                    if(alpha<.02)continue;
                    // Pull the soft edge of the enlargement back to a crisp, smooth contour. Solid interiors and
                    // fully clear areas are unchanged; only the ramp between them is steepened.
                    double edge=Smooth(alpha,.3,.7);
                    int target=(y*outW+x)*4;
                    result[target]=(byte)Math.Round(Math.Max(0,Math.Min(255,b/alpha))*edge);
                    result[target+1]=(byte)Math.Round(Math.Max(0,Math.Min(255,g/alpha))*edge);
                    result[target+2]=(byte)Math.Round(Math.Max(0,Math.Min(255,r/alpha))*edge);
                    result[target+3]=(byte)Math.Round(edge*255);
                }
            }
            var big=BitmapSource.Create(outW,outH,96,96,PixelFormats.Pbgra32,null,result,outW*4);
            big.Freeze();
            return big;
        }
        // Catmull-Rom weights for the four samples around a fractional position.
        static void Weights(double t,double[] weights)
        {
            double t2=t*t,t3=t2*t;
            weights[0]=-.5*t3+t2-.5*t;
            weights[1]=1.5*t3-2.5*t2+1;
            weights[2]=-1.5*t3+2*t2+.5*t;
            weights[3]=.5*t3-.5*t2;
        }
        static double Smooth(double value,double low,double high)
        {
            double t=Math.Max(0,Math.Min(1,(value-low)/(high-low)));
            return t*t*(3-2*t);
        }
    }
}
