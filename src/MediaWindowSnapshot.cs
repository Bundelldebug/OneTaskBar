using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class MediaWindowSnapshot
    {
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr bitmap);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]
        static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
        [DllImport("gdi32.dll")]
        static extern int SetStretchBltMode(IntPtr dc,int mode);

        // Only the foreground media window is sampled, on focus/minimize-button
        // entry. No polling capture, disk file, browser access or network request.
        internal static BitmapSource Capture(IntPtr window,Rect bounds)
        {
            if(Native.GetForegroundWindow()!=window||Native.IsIconic(window)||bounds.Width<1||bounds.Height<1||bounds.Width*bounds.Height>24000000)return null;
            var area=Rectangle.Intersect(new Rectangle((int)bounds.X,(int)bounds.Y,(int)bounds.Width,(int)bounds.Height),System.Windows.Forms.SystemInformation.VirtualScreen);
            if(area.Width<=0||area.Height<=0)return null;
            double scale=Math.Min(1,1536/Math.Max(bounds.Width,bounds.Height));
            int width=Math.Max(1,(int)Math.Round(bounds.Width*scale));
            int height=Math.Max(1,(int)Math.Round(bounds.Height*scale));
            using(var bitmap=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                using(var graphics=Graphics.FromImage(bitmap))
                {
                    graphics.Clear(System.Drawing.Color.Black);
                    var source=GetDC(IntPtr.Zero);
                    if(source==IntPtr.Zero)return null;
                    var target=graphics.GetHdc();
                    try
                    {
                        SetStretchBltMode(target,4);
                        if(!StretchBlt(target,(int)Math.Round((area.X-bounds.X)*scale),(int)Math.Round((area.Y-bounds.Y)*scale),Math.Max(1,(int)Math.Round(area.Width*scale)),Math.Max(1,(int)Math.Round(area.Height*scale)),source,area.X,area.Y,area.Width,area.Height,0x00CC0020))return null;
                    }
                    finally
                    {
                        graphics.ReleaseHdc(target);
                        ReleaseDC(IntPtr.Zero,source);
                    }
                }
                Native.RectI current;
                if(Native.GetForegroundWindow()!=window||Native.IsIconic(window)||!Native.GetWindowRect(window,out current)||
                    Math.Abs(current.Left-bounds.Left)>2||Math.Abs(current.Top-bounds.Top)>2||
                    Math.Abs(current.Right-bounds.Right)>2||Math.Abs(current.Bottom-bounds.Bottom)>2)return null;
                var handle=bitmap.GetHbitmap();
                try
                {
                    var image=Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return image;
                }
                finally {DeleteObject(handle);}
            }
        }
    }
}
