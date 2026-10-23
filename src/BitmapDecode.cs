using System;
using System.IO;
using System.Windows.Media.Imaging;
namespace OneTaskBar
{
    internal static class BitmapDecode
    {
        // Read dimensions before decoding: bound both axes, and never upscale a small source.
        internal static BitmapSource Frozen(Stream stream,int maximumEdge)
        {
            long position=stream.Position;var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation|BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);if(decoder.Frames.Count==0)return null;var frame=decoder.Frames[0];int width=frame.PixelWidth,height=frame.PixelHeight;if(width<1||height<1)return null;
            double scale=Math.Min(1,Math.Max(1,maximumEdge)/(double)Math.Max(width,height));stream.Position=position;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=Math.Max(1,(int)Math.Floor(width*scale));image.DecodePixelHeight=Math.Max(1,(int)Math.Floor(height*scale));image.StreamSource=stream;image.EndInit();image.Freeze();return image;
        }
    }
}
