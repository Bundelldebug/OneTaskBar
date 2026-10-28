using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal sealed class TrayIconVisual : FrameworkElement
    {
        readonly ImageSource source;
        readonly ImageBrush mask;
        readonly Brush ink;
        internal bool Monochrome {get{return mask!=null;}}
        internal TrayIconVisual(ImageSource image,Brush foreground=null)
        {
            source=image;ink=foreground??Paint.Text;Width=Height=26;IsHitTestVisible=false;
            var bitmap=image as BitmapSource;
            if(bitmap!=null&&IsMonochrome(bitmap)){
                mask=new ImageBrush(image) {Stretch=Stretch.Uniform};mask.Freeze();
            }
        }
        internal static bool IsMonochrome(BitmapSource bitmap)
        {
            if(bitmap.PixelWidth>256||bitmap.PixelHeight>256)return false;
            var image=new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0);
            int stride=image.PixelWidth*4,visible=0,colored=0,opaque=0;
            int minimum=255,maximum=0;
            var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);
            for(int i=0;i<pixels.Length;i+=4){
                if(pixels[i+3]<50)continue;
                visible++;
                if(Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]))-Math.Min(pixels[i],Math.Min(pixels[i+1],pixels[i+2]))>18)colored++;
                // RGB of nearly transparent edge pixels is unreliable. Classify only
                // the solid ink, preserving grayscale logos with internal shading.
                if(pixels[i+3]>=220){int luminance=(pixels[i]*114+pixels[i+1]*587+pixels[i+2]*299)/1000;minimum=Math.Min(minimum,luminance);maximum=Math.Max(maximum,luminance);opaque++;}
            }
            return visible>0&&opaque>0&&visible<image.PixelWidth*image.PixelHeight*.97&&colored<=visible/100&&maximum-minimum<=24;
        }
        protected override void OnRender(DrawingContext dc)
        {
            var bounds=new Rect(0,0,ActualWidth,ActualHeight);
            if(mask==null){dc.DrawImage(source,bounds);return;}
            dc.PushOpacityMask(mask);dc.DrawRectangle(ink,null,bounds);dc.Pop();
        }
    }
}
