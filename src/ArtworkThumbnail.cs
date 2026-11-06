using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // A small cover drawn straight from a 1280 px picture is sampled without averaging and looks jagged. This
    // draws it once, with the high-quality filter, at twice the size it is shown, and keeps that.
    internal static class ArtworkThumbnail
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource,BitmapSource> cache=new System.Runtime.CompilerServices.ConditionalWeakTable<ImageSource,BitmapSource>();
        internal static ImageSource Square(ImageSource source,int pixels)
        {
            var bitmap=source as BitmapSource;
            if(bitmap==null||bitmap.PixelWidth<=pixels&&bitmap.PixelHeight<=pixels)return source;
            BitmapSource done;
            if(cache.TryGetValue(source,out done)&&done.PixelWidth==pixels)return done;
            var visual=new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
            using(var context=visual.RenderOpen())
            {
                // Cover the square, centered, as UniformToFill does.
                double scale=Math.Max(pixels/(double)bitmap.PixelWidth,pixels/(double)bitmap.PixelHeight);
                double width=bitmap.PixelWidth*scale,height=bitmap.PixelHeight*scale;
                context.DrawImage(bitmap,new Rect((pixels-width)/2,(pixels-height)/2,width,height));
            }
            var target=new RenderTargetBitmap(pixels,pixels,96,96,PixelFormats.Pbgra32);
            target.Render(visual);
            target.Freeze();
            cache.Remove(source);
            cache.Add(source,target);
            return target;
        }
    }
}
