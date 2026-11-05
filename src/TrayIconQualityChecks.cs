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
            var larger=TrayIcons.ImproveResolution(live,original) as BitmapSource;
            Require(larger!=null&&larger.PixelWidth==64,"small snapshots use larger matching app artwork");
            Require(Object.ReferenceEquals(changed,TrayIcons.ImproveResolution(changed,original)),"larger app artwork cannot discard a different tray status");
            var packageIcon=Icon(512,Brushes.Red);
            Require(Object.ReferenceEquals(packageIcon,new TrayIconQuality(packageIcon).Choose(live,Colors.White)),"large packaged app logos remain eligible as original tray artwork");
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
            foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5})
            {
                double snapped=TrayIconVisual.Snap(26,scale)*scale;
                Require(Math.Abs(snapped-Math.Round(snapped))<1e-9,"the tray picture covers whole pixels at "+scale+"x");
            }
            var largeArt=Icon(256,Brushes.Red);
            var snapshot=Icon(20,Brushes.Red);
            var improved=TrayIcons.ImproveResolution(snapshot,largeArt) as BitmapSource;
            Require(improved!=null&&improved.PixelWidth>=96,"a matching large program picture replaces a 20 pixel snapshot");
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
            // A smooth ring on a flat tray color keeps its soft edge instead of a stair-stepped cut.
            var ringDrawing=new DrawingVisual();
            using(var dc=ringDrawing.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(32,32,36)),null,new Rect(0,0,48,48));
                dc.DrawEllipse(null,new Pen(Brushes.White,5),new Point(24,24),14,14);
            }
            var ringImage=new RenderTargetBitmap(48,48,96,96,PixelFormats.Pbgra32);ringImage.Render(ringDrawing);ringImage.Freeze();
            byte[] ringBytes;Color ringMatte;
            var extracted=TrayIconPixels.Extract(TrayFrame.For(ringImage,new Rect(0,0,48,48)),new Rect(0,0,48,48),out ringBytes,out ringMatte);
            int soft=0,solid=0;
            for(int i=3;i<ringBytes.Length;i+=4){if(ringBytes[i]>20&&ringBytes[i]<235)soft++;else if(ringBytes[i]>=235)solid++;}
            Require(extracted!=null&&soft>=40&&solid>=40,"tray icon edges keep a soft transparency: "+soft+" soft and "+solid+" solid pixels");
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
