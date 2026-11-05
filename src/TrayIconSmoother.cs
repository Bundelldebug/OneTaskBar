using System;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Tray icons arrive as 16 to 32 pixel pictures with stair-stepped edges. Showing them larger only softens the
    // steps; it does not remove them. This redraws a small icon on a finer grid: a smooth bicubic enlargement, then
    // the edge of the shape is pulled back to a clean contour, so the icon reads like a vector at any display scale.
    internal static class TrayIconSmoother
    {
        const int Target=192;
        static readonly ConditionalWeakTable<BitmapSource,BitmapSource> cache=new ConditionalWeakTable<BitmapSource,BitmapSource>();
        internal static BitmapSource Smooth(BitmapSource source)
        {
            if(source==null)return null;
            int width=source.PixelWidth,height=source.PixelHeight;
            if(Math.Max(width,height)>=96||Math.Min(width,height)<8)return source;
            BitmapSource done;
            if(cache.TryGetValue(source,out done))return done;
            done=Build(source);
            cache.Add(source,done);
            return done;
        }
        static BitmapSource Build(BitmapSource source)
        {
            var converted=new FormatConvertedBitmap(source,PixelFormats.Pbgra32,null,0);
            int w=converted.PixelWidth,h=converted.PixelHeight,stride=w*4;
            var pixels=new byte[stride*h];
            converted.CopyPixels(pixels,stride,0);
            int factor=Math.Max(2,(int)Math.Ceiling(Target/(double)Math.Max(w,h)));
            int outW=w*factor,outH=h*factor;
            var result=new byte[outW*outH*4];
            var weightX=new double[4];var weightY=new double[4];
            var channel=new double[4];
            for(int y=0;y<outH;y++)
            {
                double sy=(y+.5)/factor-.5;int y0=(int)Math.Floor(sy);Weights(sy-y0,weightY);
                for(int x=0;x<outW;x++)
                {
                    double sx=(x+.5)/factor-.5;int x0=(int)Math.Floor(sx);Weights(sx-x0,weightX);
                    channel[0]=channel[1]=channel[2]=channel[3]=0;
                    for(int j=0;j<4;j++)
                    {
                        int row=Math.Max(0,Math.Min(h-1,y0-1+j))*stride;
                        for(int i=0;i<4;i++)
                        {
                            int offset=row+Math.Max(0,Math.Min(w-1,x0-1+i))*4;
                            double weight=weightX[i]*weightY[j];
                            channel[0]+=pixels[offset]*weight;channel[1]+=pixels[offset+1]*weight;channel[2]+=pixels[offset+2]*weight;channel[3]+=pixels[offset+3]*weight;
                        }
                    }
                    double alpha=Math.Max(0,Math.Min(255,channel[3]))/255.0;
                    int target=(y*outW+x)*4;
                    if(alpha<.02)continue;
                    // Pull the soft edge of the enlargement back to a crisp, smooth contour. Solid interiors and
                    // fully clear areas are unchanged; only the ramp between them is steepened.
                    double edge=Smooth(alpha,.3,.7);
                    for(int c=0;c<3;c++)
                    {
                        double straight=Math.Max(0,Math.Min(255,channel[c]/alpha));
                        result[target+c]=(byte)Math.Round(straight*edge);
                    }
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
