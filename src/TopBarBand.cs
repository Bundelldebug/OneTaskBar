using System;
using System.Drawing;
using MediaColor = System.Windows.Media.Color;

namespace OneTaskBar
{
    internal static class TopBarBand
    {
        internal static Rectangle SampleArea(Rectangle monitor, Native.RectI frame, double scale, bool bottom)
        {
            int band = (int)Math.Ceiling(IslandSafeArea.Height * scale);
            int inset = (int)Math.Ceiling(2 * scale);
            int depth = (int)Math.Ceiling(3 * scale);
            int tolerance = (int)Math.Ceiling(5 * scale);
            if (frame.Left > monitor.Left + tolerance || frame.Right < monitor.Right - tolerance)
                return Rectangle.Empty;

            int edge = bottom ? frame.Bottom : frame.Top;
            int reservedEdge = bottom ? monitor.Bottom - band : monitor.Top + band;
            if (Math.Abs(edge - reservedEdge) > tolerance) return Rectangle.Empty;

            int x = Math.Max(frame.Left, monitor.Left);
            int right = Math.Min(frame.Right, monitor.Right);
            int y = bottom ? edge - inset - depth : edge + inset;
            if (right <= x || y < frame.Top || y + depth > frame.Bottom) return Rectangle.Empty;
            return new Rectangle(x, y, right - x, depth);
        }

        internal static MediaColor[] Capture(Rectangle area)
        {
            using (var bitmap = new Bitmap(area.Width, area.Height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(area.Location, Point.Empty, area.Size);
                return Sample(bitmap);
            }
        }

        internal static MediaColor[] Sample(Bitmap bitmap)
        {
            var colors = new MediaColor[7];
            int last = bitmap.Width - 1;
            for (int i = 0; i < colors.Length; i++)
            {
                int center = last * (2 * i + 1) / (2 * colors.Length);
                int spread = Math.Max(1, last / (colors.Length * 5));
                int red = 0, green = 0, blue = 0;
                for (int offset = -2; offset <= 2; offset++)
                {
                    var pixel = bitmap.GetPixel(Math.Max(0, Math.Min(last, center + offset * spread)), bitmap.Height / 2);
                    red += pixel.R;
                    green += pixel.G;
                    blue += pixel.B;
                }
                colors[i] = MediaColor.FromRgb((byte)(red / 5), (byte)(green / 5), (byte)(blue / 5));
            }
            return colors;
        }

        internal static bool UseLightInk(MediaColor color)
        {
            return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B) < .179;
        }

        static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
    }
}

