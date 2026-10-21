using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class AssetBuilder
{
    const string Tile="M256,16 C361,16 423,16 460,52 C496,89 496,151 496,256 C496,361 496,423 460,460 C423,496 361,496 256,496 C151,496 89,496 52,460 C16,423 16,361 16,256 C16,151 16,89 52,52 C89,16 151,16 256,16 Z";
    const string Roof="M133,240 L240,145 Q256,130 272,145 L379,240";
    const string House="M158,233 L158,330 Q158,346 174,346 L223,346 L223,283 Q223,274 232,274 L280,274 Q289,274 289,283 L289,346 L338,346 Q354,346 354,330 L354,233";
    static byte[] Icon(int size)
    {
        var visual=new DrawingVisual();using(var draw=visual.RenderOpen()) {
            draw.PushTransform(new ScaleTransform(size/512.0,size/512.0));
            var gradient=new LinearGradientBrush(Color.FromRgb(67,149,255),Color.FromRgb(17,37,104),new Point(0,0),new Point(1,1));draw.DrawGeometry(gradient,null,Geometry.Parse(Tile));
            var pen=new Pen(Brushes.White,24){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};draw.DrawGeometry(null,pen,Geometry.Parse(Roof));draw.DrawGeometry(null,pen,Geometry.Parse(House));
            draw.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(225,220,242,255)),null,new Rect(123,386,266,47),23.5,23.5);
            foreach(double x in new[]{197.0,256,315})draw.DrawEllipse(new SolidColorBrush(Color.FromRgb(23,75,139)),null,new Point(x,409.5),8.5,8.5);draw.Pop();
        }
        var target=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);target.Render(visual);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(target));using(var output=new MemoryStream()){encoder.Save(output);return output.ToArray();}
    }
    [STAThread]static void Main(string[] args)
    {
        string folder=Path.GetFullPath(args[0]);Directory.CreateDirectory(folder);
        foreach(var entry in new[]{Tuple.Create("OneTaskBar.png",512),Tuple.Create("Square150x150Logo.png",150),Tuple.Create("Square44x44Logo.png",44),Tuple.Create("StoreLogo.png",50)})File.WriteAllBytes(Path.Combine(folder,entry.Item1),Icon(entry.Item2));
        int[] sizes={16,20,24,32,40,48,64,128,256};var images=new List<byte[]>();foreach(int size in sizes)images.Add(Icon(size));
        using(var stream=File.Create(Path.Combine(folder,"OneTaskBar.ico")))using(var writer=new BinaryWriter(stream)) {writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var bytes in images)writer.Write(bytes);}
        string svg="<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 512 512'><defs><linearGradient id='blue' x2='1' y2='1'><stop stop-color='#4395ff'/><stop offset='1' stop-color='#112568'/></linearGradient></defs><path fill='url(#blue)' d='"+Tile+"'/><g fill='none' stroke='white' stroke-width='24' stroke-linecap='round' stroke-linejoin='round'><path d='"+Roof+"'/><path d='"+House+"'/></g><rect x='123' y='386' width='266' height='47' rx='23.5' fill='#dcf2ff' fill-opacity='.88'/><g fill='#174b8b'><circle cx='197' cy='409.5' r='8.5'/><circle cx='256' cy='409.5' r='8.5'/><circle cx='315' cy='409.5' r='8.5'/></g></svg>";
        File.WriteAllText(Path.Combine(folder,"OneTaskBar.svg"),svg,new UTF8Encoding(false));
    }
}
