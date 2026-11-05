using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class TrayIconPixels
    {
        internal static BitmapSource Extract(TrayFrame frame, Rect button, out byte[] bytes, out Color matte, byte[] previous=null)
        {
            bytes = null;
            matte=Colors.Transparent;
            if (button.IsEmpty || !frame.Bounds.Contains(button)) return null;
            int side = (int)Math.Floor(Math.Min(button.Width, button.Height) * .82);
            if (side < 8 || side > 256) return null;
            var crop = new Int32Rect((int)Math.Round(button.X + (button.Width - side) / 2 - frame.Bounds.X),
                (int)Math.Round(button.Y + (button.Height - side) / 2 - frame.Bounds.Y), side, side);
            if (crop.X < 0 || crop.Y < 0 || crop.X + side > frame.Image.PixelWidth || crop.Y + side > frame.Image.PixelHeight)
                return null;
            var image = new FormatConvertedBitmap(frame.Image, PixelFormats.Bgra32, null, 0);
            bytes = new byte[side * side * 4];
            image.CopyPixels(crop, bytes, side * 4, 0);
            matte=Color.FromRgb(bytes[2],bytes[1],bytes[0]);
            RemoveBackground(bytes, side);
            if(previous!=null&&bytes.SequenceEqual(previous))return null;
            int visible = 0;
            int left=side,top=side,right=-1,bottom=-1;
            for(int y=0;y<side;y++)for(int x=0;x<side;x++)
                if(bytes[(y*side+x)*4+3]!=0)
                {
                    visible++;
                    left=Math.Min(left,x);right=Math.Max(right,x);
                    top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                }
            if (visible < 4) { bytes = null; return null; }
            var result = BitmapSource.Create(side, side, 96, 96, PixelFormats.Bgra32, null, bytes, side * 4);
            result.Freeze();
            var tight=new CroppedBitmap(result,new Int32Rect(left,top,right-left+1,bottom-top+1));
            tight.Freeze();
            return tight;
        }

        static int Spread(byte[] bytes,int offset,int blue,int green,int red)
        {
            return Math.Max(Math.Abs(bytes[offset]-blue),Math.Max(Math.Abs(bytes[offset+1]-green),Math.Abs(bytes[offset+2]-red)));
        }

        // Clears the connected background, then gives the pixels at the edge a soft transparency from how
        // far each one is from the background color. Clearing them outright, as a hard cut, throws away the
        // smoothing the shell drew and leaves the stair-stepped edge seen on small tray icons.
        static void RemoveBackground(byte[] bytes, int side)
        {
            var seen = new bool[side * side];
            var queue = new Queue<int>();
            for (int i = 0; i < side; i++)
            {
                queue.Enqueue(i); queue.Enqueue((side - 1) * side + i);
                queue.Enqueue(i * side); queue.Enqueue(i * side + side - 1);
            }
            int blue = bytes[0], green = bytes[1], red = bytes[2];
            var cleared = new bool[side * side];
            while (queue.Count > 0)
            {
                int pixel = queue.Dequeue();
                if (seen[pixel]) continue;
                seen[pixel] = true;
                int offset = pixel * 4;
                if (Spread(bytes, offset, blue, green, red) > 6) continue;
                bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = bytes[offset + 3] = 0;
                cleared[pixel] = true;
                int x = pixel % side, y = pixel / side;
                if (x > 0) queue.Enqueue(pixel - 1);
                if (x + 1 < side) queue.Enqueue(pixel + 1);
                if (y > 0) queue.Enqueue(pixel - side);
                if (y + 1 < side) queue.Enqueue(pixel + side);
            }
            // How far solid artwork sits from the background: the upper quartile of what is left.
            var distances = new List<int>();
            for (int pixel = 0; pixel < side * side; pixel++)
            {
                if (cleared[pixel]) continue;
                int distance = Spread(bytes, pixel * 4, blue, green, red);
                if (distance > 40) distances.Add(distance);
            }
            if (distances.Count < 4) return;
            distances.Sort();
            double reference = Math.Max(48, distances[distances.Count * 3 / 4]);
            // Two pixels deep from the cleared area is where the shell's smoothing lies.
            var depth = new int[side * side];
            var frontier = new Queue<int>();
            for (int pixel = 0; pixel < side * side; pixel++)
                if (cleared[pixel]) frontier.Enqueue(pixel);
            while (frontier.Count > 0)
            {
                int pixel = frontier.Dequeue();
                int reached = cleared[pixel] ? 0 : depth[pixel];
                if (reached >= 2) continue;
                int x = pixel % side, y = pixel / side;
                int[] around = { x > 0 ? pixel - 1 : -1, x + 1 < side ? pixel + 1 : -1, y > 0 ? pixel - side : -1, y + 1 < side ? pixel + side : -1 };
                foreach (int next in around)
                {
                    if (next < 0 || cleared[next] || depth[next] != 0) continue;
                    depth[next] = reached + 1;
                    frontier.Enqueue(next);
                }
            }
            for (int pixel = 0; pixel < side * side; pixel++)
            {
                if (depth[pixel] == 0 || cleared[pixel]) continue;
                int offset = pixel * 4;
                double alpha = Math.Min(1, Spread(bytes, offset, blue, green, red) / reference);
                if (alpha >= .97) continue;
                if (alpha < .05)
                {
                    bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = bytes[offset + 3] = 0;
                    continue;
                }
                // Take the background back out of the blended color.
                bytes[offset] = (byte)Math.Max(0, Math.Min(255, (bytes[offset] - blue * (1 - alpha)) / alpha));
                bytes[offset + 1] = (byte)Math.Max(0, Math.Min(255, (bytes[offset + 1] - green * (1 - alpha)) / alpha));
                bytes[offset + 2] = (byte)Math.Max(0, Math.Min(255, (bytes[offset + 2] - red * (1 - alpha)) / alpha));
                bytes[offset + 3] = (byte)Math.Round(alpha * 255);
            }
        }
    }
}
