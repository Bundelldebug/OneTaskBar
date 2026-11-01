using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class MediaFlightBackdrop
    {
        internal static BitmapSource Blur(BitmapSource source,double displayWidth)
        {
            if(source==null||displayWidth<=0)return null;
            // Flatten the immutable restore cover once at its blur resolution.
            // A live effect on a full-monitor layered window is expensive even
            // when only the overlay's opacity changes during the flight.
            double factor=Math.Min(1,768.0/Math.Max(source.PixelWidth,source.PixelHeight));
            int width=Math.Max(1,(int)Math.Round(source.PixelWidth*factor));
            int height=Math.Max(1,(int)Math.Round(source.PixelHeight*factor));
            var image=new Image {Source=source,Width=width,Height=height,Stretch=Stretch.Fill,
                Effect=new BlurEffect {Radius=30*width/displayWidth,RenderingBias=RenderingBias.Quality}};
            image.Measure(new Size(width,height));
            image.Arrange(new Rect(0,0,width,height));
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
            bitmap.Render(image);
            bitmap.Freeze();
            return bitmap;
        }
    }
}
