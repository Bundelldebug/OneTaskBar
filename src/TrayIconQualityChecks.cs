using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    internal static class TrayIconQualityChecks
    {
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Tray image quality: "+message);}
        internal static void Run()
        {
            var original=Icon(64,Brushes.Red);
            var live=Icon(16,Brushes.Red);
            var quality=new TrayIconQuality(original);
            Require(Object.ReferenceEquals(original,quality.Choose(live,Colors.White)),"matching current state retains the original pixels");
            var changed=Icon(16,Brushes.Lime);
            Require(Object.ReferenceEquals(changed,quality.Choose(changed,Colors.White)),"a new status color cannot be replaced by a stale original");
            var badge=Icon(16,Brushes.Red,true);
            Require(Object.ReferenceEquals(badge,quality.Choose(badge,Colors.White)),"a changed status badge stays live");
            var black=Icon(64,Brushes.Black);
            var white=Icon(16,Brushes.White);
            Require(Object.ReferenceEquals(black,new TrayIconQuality(black).Choose(white,Colors.Black)),"neutral tray ink can differ between Windows themes without losing resolution");
            using(var stream=new MemoryStream())
            {
                var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(live));png.Save(stream);
                var decoded=TrayIcons.Decode(stream.ToArray()) as BitmapSource;
                Require(decoded!=null&&decoded.PixelWidth==16,"decoding does not disguise upscaling as a higher resolution original");
            }
            var drawing=new DrawingVisual();
            using(var dc=drawing.RenderOpen())
            {
                dc.DrawEllipse(Brushes.Black,null,new Point(16,16),12,12);
                dc.DrawEllipse(Brushes.White,null,new Point(16,16),6,6);
            }
            var detailed=new RenderTargetBitmap(32,32,96,96,PixelFormats.Pbgra32);detailed.Render(drawing);detailed.Freeze();
            var visual=new TrayIconVisual(detailed,Brushes.White);
            Require(!visual.Monochrome&&visual.LightPlate,"dark multitone logos gain a fixed light backing while retaining their colors");
            var mono=new TrayIconVisual(black,Brushes.White);
            mono.Measure(new Size(26,26));mono.Arrange(new Rect(0,0,26,26));
            var rendered=new RenderTargetBitmap(26,26,96,96,PixelFormats.Pbgra32);rendered.Render(mono);
            var center=new byte[4];rendered.CopyPixels(new Int32Rect(13,13,1,1),center,4,0);
            Require(mono.Monochrome&&center[0]>230&&center[1]>230&&center[2]>230,"a black monochrome icon renders with the selected popup ink");
        }
        static BitmapSource Icon(int size,Brush color,bool badge=false)
        {
            var drawing=new DrawingVisual();
            using(var dc=drawing.RenderOpen())
            {
                dc.DrawEllipse(color,null,new Point(size/2.0,size/2.0),size*.375,size*.375);
                if(badge)dc.DrawRectangle(Brushes.Lime,null,new Rect(size*.625,size*.625,size*.25,size*.25));
            }
            var image=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);image.Render(drawing);image.Freeze();return image;
        }
    }
}
