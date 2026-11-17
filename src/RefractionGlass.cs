using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace OneTaskBar
{
    // Optional real refraction for small, short-lived surfaces (notification banners). The pixels behind the surface
    // are read once, and the surface is drawn from them with the Prism glass profile: a quarter-circle bevel plus an
    // outer lip bend the backdrop along the edge normal, then vibrance, tint, a dark inner line and a light-facing rim.
    // WPF has no live refracting backdrop, so this is a still of what was behind the surface when it appeared.
    internal static class RefractionGlass
    {
        // Prism "menu" preset, in dp.
        internal const double Refraction=17,Edge=26,OuterWidth=7,OuterRefraction=12,RimStrength=.4,Vibrance=1.35;
        const double LightX=-.45,LightY=-.89;
        internal static bool Enabled {get{return Preferences.Current.LiquidRefraction&&LiquidGlass.Enabled;}}
        // How far past the surface the backdrop is sampled, in dp: the most the edge can bend it.
        internal static double Margin {get{return Math.Ceiling(Refraction+OuterRefraction)+2;}}
        static double Clamp(double value,double low,double high){return value<low?low:value>high?high:value;}
        static double Smooth(double a,double b,double x){double t=Clamp((x-a)/(b-a),0,1);return t*t*(3-2*t);}
        // 1 at the very edge, falling to 0 at depth >= width: 1 - sqrt(1 - (1 - u)^2).
        internal static double Bevel(double depth,double width)
        {
            if(width<=0)return 0;
            double u=Clamp(depth/width,0,1),v=1-u;
            return 1-Math.Sqrt(Math.Max(0,1-v*v));
        }
        // Signed distance to a rounded rectangle centred on the origin (negative inside) and its outward normal.
        internal static double Distance(double x,double y,double halfWidth,double halfHeight,double radius,out double nx,out double ny)
        {
            double qx=Math.Abs(x)-(halfWidth-radius),qy=Math.Abs(y)-(halfHeight-radius);
            double ox=Math.Max(qx,0),oy=Math.Max(qy,0),outer=Math.Sqrt(ox*ox+oy*oy);
            double sx=x<0?-1:1,sy=y<0?-1:1;
            if(outer>1e-6){nx=sx*ox/outer;ny=sy*oy/outer;}
            else if(qx>qy){nx=sx;ny=0;}
            else{nx=0;ny=sy;}
            return outer+Math.Min(Math.Max(qx,qy),0)-radius;
        }
        // Reads the screen around a surface. rect is the surface in device pixels; the result also covers the margin.
        internal static Bitmap Capture(Rectangle rect,int margin,out Rectangle area)
        {
            var wanted=Rectangle.Inflate(rect,margin,margin);
            Rectangle screen=Forms.SystemInformation.VirtualScreen;
            area=Rectangle.Intersect(wanted,screen);
            if(area.Width<1||area.Height<1)return null;
            var bitmap=new Bitmap(area.Width,area.Height,PixelFormat.Format32bppRgb);
            try
            {
                using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(area.Location,Point.Empty,area.Size);
                return bitmap;
            }
            catch{bitmap.Dispose();return null;}
        }
        static int[] Pixels(Bitmap bitmap)
        {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppRgb);
            try
            {
                var values=new int[bitmap.Width*bitmap.Height];
                for(int y=0;y<bitmap.Height;y++)System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),values,y*bitmap.Width,bitmap.Width);
                return values;
            }
            finally{bitmap.UnlockBits(data);}
        }
        // Two box-blur passes per axis approximate a soft Gaussian.
        internal static int[] Blur(int[] source,int width,int height,int radius)
        {
            if(radius<1)return source;
            var first=new int[source.Length];var second=new int[source.Length];
            Box(source,first,width,height,radius,true);
            Box(first,second,width,height,radius,false);
            Box(second,first,width,height,radius,true);
            Box(first,second,width,height,radius,false);
            return second;
        }        static void Box(int[] from,int[] to,int width,int height,int radius,bool horizontal)
        {
            int length=horizontal?width:height,lines=horizontal?height:width,span=radius*2+1;
            for(int line=0;line<lines;line++)
            {
                int r=0,g=0,b=0;
                for(int k=-radius;k<=radius;k++)
                {
                    int i=Math.Max(0,Math.Min(length-1,k)),p=from[Index(i,line,width,horizontal)];
                    r+=(p>>16)&255;g+=(p>>8)&255;b+=p&255;
                }
                for(int i=0;i<length;i++)
                {
                    to[Index(i,line,width,horizontal)]=((r/span)<<16)|((g/span)<<8)|(b/span);
                    int add=from[Index(Math.Min(length-1,i+radius+1),line,width,horizontal)],drop=from[Index(Math.Max(0,i-radius),line,width,horizontal)];
                    r+=((add>>16)&255)-((drop>>16)&255);g+=((add>>8)&255)-((drop>>8)&255);b+=(add&255)-(drop&255);
                }
            }
        }
        static int Index(int along,int line,int width,bool horizontal){return horizontal?line*width+along:along*width+line;}
        // Draws the glass. width/height/radius are the surface in device pixels; scale converts dp to pixels.
        // rim: only the edge band is drawn and it fades out inward, to lie over a live blur that fills the middle.
        internal static BitmapSource Render(Bitmap backdrop,Rectangle area,Rectangle surface,double radius,double scale,bool dark,bool rim=false)
        {
            if(backdrop==null||surface.Width<8||surface.Height<8)return null;
            int w=surface.Width,h=surface.Height,aw=backdrop.Width,ah=backdrop.Height;
            var pixels=Blur(Pixels(backdrop),aw,ah,Math.Max(1,(int)Math.Round(2*scale)));
            var output=new byte[w*h*4];
            double halfWidth=w/2.0,halfHeight=h/2.0,refr=Refraction*scale,edge=Edge*scale,outerW=OuterWidth*scale,outerR=OuterRefraction*scale;
            // Prism's high level blur (2) with a tint between its high and medium steps, so the bend shows but light text stays readable.
            double tintR=dark?28:250,tintG=dark?28:250,tintB=dark?30:252,tintA=dark?.5:.5;
            double edgeDark=dark?.32*.4:.32;
            for(int y=0;y<h;y++)
            for(int x=0;x<w;x++)
            {
                double px=x+.5-halfWidth,py=y+.5-halfHeight,nx,ny,d=Distance(px,py,halfWidth,halfHeight,radius,out nx,out ny);
                double coverage=Clamp(.5-d,0,1);
                int o=(y*w+x)*4;
                if(coverage<=0)continue;
                double depth=Math.Max(-d,0),lip=1-Smooth(0,outerW,depth);
                double fade=rim?1-Smooth(edge*.4,edge*.95,depth):1;
                if(fade<=0)continue;
                coverage*=fade;
                double bend=refr*Bevel(depth,edge)+outerR*Bevel(depth,outerW)*lip;
                double sx=surface.X+x+.5+nx*bend-area.X,sy=surface.Y+y+.5+ny*bend-area.Y;
                double fx=Clamp(sx-.5,0,aw-1),fy=Clamp(sy-.5,0,ah-1);
                int x0=(int)fx,y0=(int)fy,x1=Math.Min(aw-1,x0+1),y1=Math.Min(ah-1,y0+1);
                double tx=fx-x0,ty=fy-y0;
                double r=0,g=0,b=0;
                for(int k=0;k<4;k++)
                {
                    int p=pixels[(k<2?y0:y1)*aw+((k&1)==0?x0:x1)];
                    double weight=((k&1)==0?1-tx:tx)*(k<2?1-ty:ty);
                    r+=((p>>16)&255)*weight;g+=((p>>8)&255)*weight;b+=(p&255)*weight;
                }
                double luma=.2126*r+.7152*g+.0722*b;
                r=luma+(r-luma)*Vibrance;g=luma+(g-luma)*Vibrance;b=luma+(b-luma)*Vibrance;
                r+=(tintR-r)*tintA;g+=(tintG-g)*tintA;b+=(tintB-b)*tintA;
                double shine=Math.Max(0,nx*LightX+ny*LightY),line=1-Smooth(0,1.4*scale,depth),mask=1-Smooth(0,2.2*scale,depth);
                double darken=1-edgeDark*line*(1-shine);
                r*=darken;g*=darken;b*=darken;
                double glow=RimStrength*mask*(.25+.75*shine)*255;
                r+=glow;g+=glow;b+=glow;
                output[o]=(byte)Clamp(b,0,255);output[o+1]=(byte)Clamp(g,0,255);output[o+2]=(byte)Clamp(r,0,255);output[o+3]=(byte)Math.Round(coverage*255);
                // Premultiply for the WPF format.
                if(coverage<1){output[o]=(byte)(output[o]*coverage);output[o+1]=(byte)(output[o+1]*coverage);output[o+2]=(byte)(output[o+2]*coverage);}
            }
            var image=BitmapSource.Create(w,h,96*scale,96*scale,System.Windows.Media.PixelFormats.Pbgra32,null,output,w*4);
            image.Freeze();return image;
        }
    }
}
