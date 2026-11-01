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

        static void RemoveBackground(byte[] bytes, int side)
        {
            // Clear connected background only; retain enclosed white logo details.
            var seen = new bool[side * side];
            var queue = new Queue<int>();
            for (int i = 0; i < side; i++)
            {
                queue.Enqueue(i); queue.Enqueue((side - 1) * side + i);
                queue.Enqueue(i * side); queue.Enqueue(i * side + side - 1);
            }
            int blue = bytes[0], green = bytes[1], red = bytes[2];
            while (queue.Count > 0)
            {
                int pixel = queue.Dequeue();
                if (seen[pixel]) continue;
                seen[pixel] = true;
                int offset = pixel * 4;
                if (Math.Abs(bytes[offset] - blue) > 18 || Math.Abs(bytes[offset + 1] - green) > 18 ||
                    Math.Abs(bytes[offset + 2] - red) > 18) continue;
                bytes[offset] = bytes[offset + 1] = bytes[offset + 2] = bytes[offset + 3] = 0;
                int x = pixel % side, y = pixel / side;
                if (x > 0) queue.Enqueue(pixel - 1);
                if (x + 1 < side) queue.Enqueue(pixel + 1);
                if (y > 0) queue.Enqueue(pixel - side);
                if (y + 1 < side) queue.Enqueue(pixel + side);
            }
        }
    }
}
