using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Read the tray's own client pixels. This does not register a shell callback
    // or capture the desktop behind an invisible Explorer window.
    internal sealed class TrayFrame
    {
        internal readonly BitmapSource Image;
        internal readonly Rect Bounds;

        TrayFrame(BitmapSource image, Native.RectI bounds)
        {
            Image = image;
            Bounds = new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        }

        [DllImport("user32.dll")]
        static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);

        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr value);

        internal static TrayFrame Read(IntPtr window)
        {
            Native.RectI bounds;
            if (!Native.IsWindow(window) || !Native.GetWindowRect(window, out bounds)) return null;
            int width = bounds.Right - bounds.Left, height = bounds.Bottom - bounds.Top;
            if (width < 1 || height < 1 || width > 2048 || height > 2048) return null;
            using (var bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                var dc = graphics.GetHdc();
                bool printed;
                try { printed = PrintWindow(window, dc, 2); }
                finally { graphics.ReleaseHdc(dc); }
                if (!printed) return null;
                var handle = bitmap.GetHbitmap();
                try
                {
                    var image = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    image.Freeze();
                    return new TrayFrame(image, bounds);
                }
                finally { DeleteObject(handle); }
            }
        }
    }
}
