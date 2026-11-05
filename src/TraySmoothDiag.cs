using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OneTaskBar
{
    // Manual: draws small hard-edged icons as the tray gives them, plain and smoothed, at tray size and enlarged.
    internal static class TraySmoothDiag
    {
        internal static int Run()
        {
            var app=new Application();
            var panel=new StackPanel {Orientation=Orientation.Vertical,Background=Brushes.DimGray};
            foreach(int size in new[]{16,24,32})
            {
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen())
                {
                    RenderOptions.SetEdgeMode(visual,EdgeMode.Aliased);
                    dc.DrawEllipse(Brushes.White,null,new Point(size/2.0,size/2.0),size*.42,size*.42);
                    dc.DrawEllipse(Brushes.Black,null,new Point(size/2.0,size/2.0),size*.18,size*.18);
                    dc.DrawRectangle(Brushes.OrangeRed,null,new Rect(size*.1,size*.75,size*.8,size*.12));
                    dc.DrawLine(new Pen(Brushes.Cyan,1),new Point(0,0),new Point(size,size));
                }
                var raw=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);raw.Render(visual);raw.Freeze();
                var row=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(8)};
                foreach(var image in new BitmapSource[]{raw,TrayIconSmoother.Smooth(raw)})
                {
                    var view=new Image {Source=image,Width=46,Height=46,Margin=new Thickness(8)};
                    RenderOptions.SetBitmapScalingMode(view,BitmapScalingMode.HighQuality);
                    row.Children.Add(view);
                    var big=new Image {Source=image,Width=160,Height=160,Margin=new Thickness(8)};
                    RenderOptions.SetBitmapScalingMode(big,BitmapScalingMode.HighQuality);
                    row.Children.Add(big);
                }
                panel.Children.Add(row);
            }
            panel.Measure(new Size(900,700));panel.Arrange(new Rect(0,0,panel.DesiredSize.Width,panel.DesiredSize.Height));
            var target=new RenderTargetBitmap((int)panel.DesiredSize.Width,(int)panel.DesiredSize.Height,96,96,PixelFormats.Pbgra32);target.Render(panel);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));
            Directory.CreateDirectory(AppLog.Data);
            using(var stream=File.Create(Path.Combine(AppLog.Data,"tray-smooth.png")))encoder.Save(stream);
            return 0;
        }
    }
}
