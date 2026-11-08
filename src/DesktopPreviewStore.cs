using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Remembers what each virtual desktop looked like the last time it was in front. Only the desktop that is
    // current when the overview opens is sampled (one small screen copy); other desktops keep their last picture.
    internal static class DesktopPreviewStore
    {
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr bitmap);
        [DllImport("user32.dll")]static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")]static extern int ReleaseDC(IntPtr window,IntPtr dc);
        [DllImport("gdi32.dll")]static extern bool StretchBlt(IntPtr target,int x,int y,int width,int height,IntPtr source,int sx,int sy,int sw,int sh,uint operation);
        [DllImport("gdi32.dll")]static extern int SetStretchBltMode(IntPtr dc,int mode);
        const int Width=360;
        static readonly Dictionary<Guid,BitmapSource> images=new Dictionary<Guid,BitmapSource>();

        internal static BitmapSource Find(Guid desktop)
        {
            BitmapSource image;
            return images.TryGetValue(desktop,out image)?image:null;
        }

        internal static void Prune(IEnumerable<Guid> alive)
        {
            var keep=new HashSet<Guid>(alive);
            foreach(var gone in images.Keys.Where(k=>!keep.Contains(k)).ToArray())images.Remove(gone);
        }

        internal static void Capture(Guid desktop,Rectangle monitor)
        {
            if(desktop==Guid.Empty||monitor.Width<64||monitor.Height<64)return;
            try
            {
                int height=Math.Max(1,(int)Math.Round(Width*(double)monitor.Height/monitor.Width));
                using(var bitmap=new Bitmap(Width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                {
                    using(var graphics=Graphics.FromImage(bitmap))
                    {
                        var source=GetDC(IntPtr.Zero);
                        if(source==IntPtr.Zero)return;
                        var target=graphics.GetHdc();
                        try
                        {
                            SetStretchBltMode(target,4);
                            if(!StretchBlt(target,0,0,Width,height,source,monitor.X,monitor.Y,monitor.Width,monitor.Height,0x00CC0020))return;
                        }
                        finally
                        {
                            graphics.ReleaseHdc(target);
                            ReleaseDC(IntPtr.Zero,source);
                        }
                    }
                    var handle=bitmap.GetHbitmap();
                    try
                    {
                        var image=Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
                        image.Freeze();
                        images[desktop]=image;
                    }
                    finally {DeleteObject(handle);}
                }
            }
            catch(Exception error)
            {
                AppLog.Once("desktop-preview",error.GetType().Name);
            }
        }
    }
}
