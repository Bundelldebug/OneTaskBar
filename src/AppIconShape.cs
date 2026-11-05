using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class AppIconShape
    {
        sealed class Analysis
        {
            internal bool IsSquircle, FullArtwork;
            internal Color? EdgeColor, Background;
            internal Rect? FilledBox;
        }

        // Weak keys do not keep retired source bitmaps alive. Values contain only
        // shape/color metadata; the temporary analysis pixels are discarded.
        static readonly ConditionalWeakTable<ImageSource, Analysis> analyses = new ConditionalWeakTable<ImageSource, Analysis>();
        static readonly ConditionalWeakTable<ImageSource, Analysis>.CreateValueCallback inspect = Analyze;
        internal static int AnalysisCount { get; private set; }

        static Analysis Inspect(ImageSource source)
        {
            return source.IsFrozen ? analyses.GetValue(source, inspect) : Analyze(source);
        }

        static Analysis Analyze(ImageSource source)
        {
            AnalysisCount++;
            byte[] data = Pixels(Raster(source, 64));
            var value = new Analysis
            {
                IsSquircle = DetectSquircle(data)
            };
            int opaque = 0, edgeCount = 0, best = 0;
            var count = new int[512];
            var red = new int[512];
            var green = new int[512];
            var blue = new int[512];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    int offset = (y * 64 + x) * 4;
                    if (data[offset + 3] < 224)
                        continue;
                    opaque++;
                    bool boundary = x < 2 || y < 2 || x > 61 || y > 61;
                    if (!boundary)
                        boundary = data[(y * 64 + x - 2) * 4 + 3] < 224 || data[(y * 64 + x + 2) * 4 + 3] < 224 || data[((y - 2) * 64 + x) * 4 + 3] < 224 || data[((y + 2) * 64 + x) * 4 + 3] < 224;
                    if (!boundary)
                        continue;
                    int bucket = (data[offset + 2] >> 5) * 64 + (data[offset + 1] >> 5) * 8 + (data[offset] >> 5);
                    count[bucket]++;
                    red[bucket] += data[offset + 2];
                    green[bucket] += data[offset + 1];
                    blue[bucket] += data[offset];
                    edgeCount++;
                    if (count[bucket] > count[best])
                        best = bucket;
                }

            value.FilledBox = FindFilledBox(data);
            if ((opaque > 4096 * .68 || value.FilledBox.HasValue) && edgeCount > 0 && count[best] > edgeCount * .35)
                value.EdgeColor = Color.FromRgb((byte)(red[best] / count[best]), (byte)(green[best] / count[best]), (byte)(blue[best] / count[best]));
            value.FullArtwork = opaque > 4096 * .96;
            return value;
        }

        // The drawn area of artwork that is itself a square or rounded square, in 64px sample
        // coordinates. A circle fills about 79% of its bounds, so it stays on a tile instead.
        static Rect? FindFilledBox(byte[] data)
        {
            int left = 64, top = 64, right = -1, bottom = -1, solid = 0;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data[(y * 64 + x) * 4 + 3] < 112)
                        continue;
                    solid++;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }

            int w = right - left + 1, h = bottom - top + 1;
            if (w < 16 || h < 16 || Math.Abs(w - h) > Math.Max(w, h) * .12)
                return null;
            if (solid / (double)(w * h) < .86)
                return null;
            return new Rect(left, top, w, h);
        }

        internal static bool FillsTile(ImageSource source)
        {
            return source != null && Inspect(source).FilledBox.HasValue;
        }

        internal static BitmapSource Raster(ImageSource source, int pixels)
        {
            if (source == null)
                return null;
            pixels = Math.Max(16, Math.Min(512, pixels));
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
            using (var draw = visual.RenderOpen())
            {
                double w = source.Width, h = source.Height;
                if (Double.IsNaN(w) || Double.IsNaN(h) || Double.IsInfinity(w) || Double.IsInfinity(h) || w <= 0 || h <= 0)
                {
                    w = h = pixels;
                }

                double scale = pixels / Math.Max(w, h);
                w *= scale;
                h *= scale;
                draw.DrawImage(source, new Rect((pixels - w) / 2, (pixels - h) / 2, w, h));
            }

            var bitmap = new RenderTargetBitmap(pixels, pixels, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        internal static bool AlreadySquircle(ImageSource source)
        {
            return source != null && Inspect(source).IsSquircle;
        }

        static bool DetectSquircle(byte[] data)
        {
            int left = 64, top = 64, right = -1, bottom = -1;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    if (data[(y * 64 + x) * 4 + 3] < 112)
                        continue;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }

            int w = right - left + 1, h = bottom - top + 1;
            if (w < 16 || h < 16 || Math.Abs(w - h) > Math.Max(w, h) * .12)
                return false;
            int solid = 0;
            for (int y = top; y <= bottom; y++)
                for (int x = left; x <= right; x++)
                    if (data[(y * 64 + x) * 4 + 3] > 112)
                        solid++;
            // A circle occupies about 79% of its bounds; a continuous-corner tile occupies more.
            if (solid / (double)(w * h) < .855)
                return false;
            int corners = 0;
            foreach (int x in new[]
            {
                left + 1,
                right - 1
            }

            )
                foreach (int y in new[]
                {
                    top + 1,
                    bottom - 1
                }

                )
                {
                    if (data[(y * 64 + x) * 4 + 3] > 112)
                        corners++;
                }

            if (corners > 1)
                return false;
            // Match the bowed sides in the reference, rather than treating every
            // rounded square as a squircle. The boundary is about 9% inset at 1/8 height.
            int probeY = top + Math.Max(1, (int)Math.Round((h - 1) * .125));
            int edge = left;
            while (edge <= right && data[(probeY * 64 + edge) * 4 + 3] < 112)
                edge++;
            double edgeInset = (edge - left) / (double)w;
            if (edgeInset < .045 || edgeInset > .135)
                return false;
            int inset = Math.Max(1, (int)Math.Round(w * .13));
            foreach (int y in new[]
            {
                top + (int)(h * .12),
                bottom - (int)(h * .12)
            }

            )
            {
                if (data[(y * 64 + left + inset) * 4 + 3] < 112 || data[(y * 64 + right - inset) * 4 + 3] < 112)
                    return false;
            }

            foreach (int x in new[]
            {
                left + (int)(w * .12),
                right - (int)(w * .12)
            }

            )
            {
                if (data[((top + inset) * 64 + x) * 4 + 3] < 112 || data[((bottom - inset) * 64 + x) * 4 + 3] < 112)
                    return false;
            }

            return true;
        }

        internal static byte[] Pixels(BitmapSource bitmap)
        {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[converted.PixelWidth * converted.PixelHeight * 4];
            converted.CopyPixels(bytes, converted.PixelWidth * 4, 0);
            return bytes;
        }

        internal static Color Background(ImageSource source)
        {
            if (source == null)
                return Color.FromRgb(54, 75, 100);
            Analysis analysis = Inspect(source);
            if (!analysis.Background.HasValue)
                analysis.Background = ReadBackground(source);
            return analysis.Background.Value;
        }

        static Color ReadBackground(ImageSource source)
        {
            // Artwork that is not a filled shape sits on white. Only a mark that would
            // disappear on white, because nearly all of it is white, gets a black tile.
            byte[] bytes = Pixels(Raster(source, 64));
            int visible = 0, contrasting = 0;
            for (int i = 0; i < bytes.Length; i += 4)
            {
                if (bytes[i + 3] < 112)
                    continue;
                visible++;
                double light = (bytes[i + 2] * .213 + bytes[i + 1] * .715 + bytes[i] * .072) / 255;
                if (light < .8)
                    contrasting++;
            }

            bool invisibleOnWhite = visible > 0 && contrasting < visible * .06;
            return invisibleOnWhite ? Colors.Black : Colors.White;
        }

        internal static ImageSource Automatic(ImageSource source, bool compact = false)
        {
            if (source == null)
                return null;
            Analysis analysis = Inspect(source);
            if (analysis.FilledBox.HasValue)
                return Filled(source, analysis);
            if (analysis.EdgeColor.HasValue)
                return Tile(source, analysis.EdgeColor.Value.ToString(), compact ? 92 : 100);
            return Tile(source, null, analysis.FullArtwork ? (compact ? 92 : 100) : (compact ? 72 : 78));
        }

        // Square artwork becomes the tile: its drawn area, without any transparent margin in the
        // file, reaches past the outline so no background shows along the edges.
        static ImageSource Filled(ImageSource source, Analysis analysis)
        {
            const double reach = 96 * 1.04;
            Rect box = analysis.FilledBox.Value;
            double w = source.Width, h = source.Height;
            if (w <= 0 || h <= 0 || Double.IsNaN(w) || Double.IsNaN(h) || Double.IsInfinity(w) || Double.IsInfinity(h))
            {
                w = h = 64;
            }

            double sample = 64 / Math.Max(w, h);
            double scale = reach / Math.Min(box.Width, box.Height);
            double drawnWidth = w * sample * scale, drawnHeight = h * sample * scale;
            double x = 50 + ((64 - w * sample) / 2 - (box.Left + box.Width / 2)) * scale;
            double y = 50 + ((64 - h * sample) / 2 - (box.Top + box.Height / 2)) * scale;
            Color tint = analysis.EdgeColor ?? Background(source);
            var group = new DrawingGroup();
            using (var draw = group.Open())
            {
                var shape = SquircleGeometry.Create(new Rect(2, 2, 96, 96));
                draw.DrawGeometry(new SolidColorBrush(tint), null, shape);
                draw.PushClip(shape);
                draw.DrawImage(source, new Rect(x, y, drawnWidth, drawnHeight));
                draw.Pop();
            }

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        internal static ImageSource Tile(ImageSource source, string color, int artworkPercent)
        {
            if (source == null)
                return null;
            Color tint = default(Color);
            bool explicitColor = false;
            if (!String.IsNullOrEmpty(color))
            {
                try
                {
                    tint = (Color)ColorConverter.ConvertFromString(color);
                    explicitColor = true;
                }
                catch (FormatException)
                {
                }
            }

            if (!explicitColor)
                tint = Background(source);
            double amount = Math.Max(40, Math.Min(100, artworkPercent)) / 100.0;
            var group = new DrawingGroup();
            // Keep the native ImageSource in the drawing; the 32/64px analysis
            // samples above never become the displayed or exported artwork.
            using (var draw = group.Open())
            {
                var shape = SquircleGeometry.Create(new Rect(2, 2, 96, 96));
                draw.DrawGeometry(new SolidColorBrush(tint), null, shape);
                draw.PushClip(shape);
                double w = source.Width, h = source.Height;
                if (w <= 0 || h <= 0 || Double.IsNaN(w) || Double.IsNaN(h) || Double.IsInfinity(w) || Double.IsInfinity(h))
                {
                    w = h = 100;
                }

                double scale = 96 * amount / Math.Max(w, h);
                w *= scale;
                h *= scale;
                draw.DrawImage(source, new Rect(50 - w / 2, 50 - h / 2, w, h));
                draw.Pop();
            }

            group.Freeze();
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }
    }
}
