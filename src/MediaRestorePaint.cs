using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // After a restore flight the plane still shows the window's picture, because a browser paints its page white for a
    // moment after leaving the minimized state. This tells when the real window has drawn its page again, by comparing how
    // much of it is blank white with the picture taken before it was minimized: a page that was white to begin with is
    // released at once, a video page waits until the video is back.
    internal static class MediaRestorePaint
    {
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
        const int Columns=24,Rows=16;

        // The share of sampled points that are near white in the picture taken before the minimize.
        static double WhiteShare(BitmapSource image)
        {
            if(image==null||image.PixelWidth<Columns||image.PixelHeight<Rows)return 1;
            var source=image.Format==PixelFormats.Bgr32||image.Format==PixelFormats.Bgra32?image:new FormatConvertedBitmap(image,PixelFormats.Bgr32,null,0);
            var pixel=new byte[4];
            int white=0;
            for(int row=0;row<Rows;row++)
            {
                for(int column=0;column<Columns;column++)
                {
                    int x=Sample(column,Columns,source.PixelWidth,.05,.95),y=Sample(row,Rows,source.PixelHeight,.15,.95);
                    source.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);
                    if(White(pixel[2],pixel[1],pixel[0]))white++;
                }
            }
            return white/(double)(Columns*Rows);
        }

        // Waits, off the interface thread, until the window is no whiter than its earlier picture allows, or the time is up.
        internal static Task<bool> Painted(IntPtr window,BitmapSource picture,int limitMilliseconds)
        {
            return Task.Run(async ()=>
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();
                double before;
                try{before=WhiteShare(picture);}
                catch{return false;}
                Bitmap bitmap=null;
                try
                {
                    while(clock.ElapsedMilliseconds<limitMilliseconds)
                    {
                        Native.RectI rect;
                        if(!Native.GetWindowRect(window,out rect)||Native.IsIconic(window))return false;
                        int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
                        if(width<Columns||height<Rows)return false;
                        if(bitmap==null||bitmap.Width!=width||bitmap.Height!=height)
                        {
                            if(bitmap!=null)bitmap.Dispose();
                            bitmap=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppRgb);
                        }
                        using(var graphics=Graphics.FromImage(bitmap))
                        {
                            var dc=graphics.GetHdc();
                            bool drawn;
                            try{drawn=PrintWindow(window,dc,2);}
                            finally{graphics.ReleaseHdc(dc);}
                            if(!drawn)return false;
                        }
                        if(WhiteShare(bitmap)<=before+.12)return true;
                        await Task.Delay(40);
                    }
                    return false;
                }
                catch{return false;}
                finally{if(bitmap!=null)bitmap.Dispose();}
            });
        }

        static double WhiteShare(Bitmap bitmap)
        {
            var data=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),System.Drawing.Imaging.ImageLockMode.ReadOnly,System.Drawing.Imaging.PixelFormat.Format32bppRgb);
            try
            {
                int white=0;
                for(int row=0;row<Rows;row++)
                {
                    for(int column=0;column<Columns;column++)
                    {
                        int x=Sample(column,Columns,bitmap.Width,.05,.95),y=Sample(row,Rows,bitmap.Height,.15,.95);
                        int value=Marshal.ReadInt32(data.Scan0,y*data.Stride+x*4);
                        if(White((byte)(value>>16),(byte)(value>>8),(byte)value))white++;
                    }
                }
                return white/(double)(Columns*Rows);
            }
            finally{bitmap.UnlockBits(data);}
        }

        static int Sample(int index,int count,int size,double start,double end)
        {
            double share=start+(end-start)*(index+.5)/count;
            return Math.Max(0,Math.Min(size-1,(int)(share*size)));
        }

        static bool White(byte red,byte green,byte blue){return red>244&&green>244&&blue>244;}
    }
}
