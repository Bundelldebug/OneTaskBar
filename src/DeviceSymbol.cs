using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace OneTaskBar
{
    // Runtime alpha mask from the installed device app. No OEM asset is embedded.
    internal sealed class DeviceSymbol
    {
        internal ImageSource Source {get;private set;}
        readonly Dictionary<Brush,ImageSource> colors=new Dictionary<Brush,ImageSource>();
        internal bool Set(ImageSource image){if(Source==image)return false;Source=image;colors.Clear();return true;}
        internal void Draw(DrawingContext dc,Brush color,Point center,double size,bool muted)
        {
            ImageSource image;if(!colors.TryGetValue(color,out image)){var group=new DrawingGroup {OpacityMask=new ImageBrush(Source){Stretch=Stretch.Uniform}};using(var drawing=group.Open())drawing.DrawRectangle(color,null,new Rect(0,0,24,24));var tinted=new DrawingImage(group);if(tinted.CanFreeze)tinted.Freeze();if(colors.Count>=4)colors.Clear();colors[color]=image=tinted;}
            var bounds=new Rect(center.X-size/2,center.Y-size/2,size,size);dc.DrawImage(image,bounds);
            if(muted){var pen=new Pen(color,Math.Max(1,size/12)){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};dc.DrawLine(pen,new Point(bounds.Left+1,bounds.Top+1),new Point(bounds.Right-1,bounds.Bottom-1));}
        }
    }
}
