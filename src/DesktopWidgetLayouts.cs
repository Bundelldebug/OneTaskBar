using System;
using System.Collections.Generic;
using System.Windows;

namespace OneTaskBar
{
    // The arrangement offered the first time widgets are turned on. Like the Mac desktop,
    // it is one tidy block on the right-hand edge, on the same cells as every tile, with
    // square tiles above and wide ones below. Rows that do not fit the screen are left out.
    internal static class DesktopWidgetLayouts
    {
        internal const double Margin=24;
        internal static double BlockWidth {get{return InfoTile.Span(4);}}
        internal static List<KeyValuePair<string,Point>> Default(Size available,bool battery,bool weather)
        {
            var result=new List<KeyValuePair<string,Point>>();
            double width=BlockWidth;
            if(available.Width<width+Margin*2)return result;
            double left=available.Width-Margin-width,top=Margin;
            var rows=new List<string[]>
            {
                new[]{"clock-digital:2x2","calendar:2x2"},
                new[]{weather?"weather:2x2":"todo:2x2",battery?"power-list:2x2":"care:2x2"},
                new[]{"player:4x1"},
                new[]{"search:4x1"},
                new[]{battery?"care:4x1":"clock-dual:4x1"}
            };
            double y=top;
            foreach(var row in rows)
            {
                double height=InfoTile.Span(InfoTileCatalog.Rows(row[0]));
                if(y+height>available.Height-Margin)break;
                double x=left;
                foreach(string key in row)
                {
                    result.Add(new KeyValuePair<string,Point>(key,new Point(x,y)));
                    x+=InfoTile.Span(InfoTileCatalog.Columns(key))+InfoTile.Gap;
                }
                y+=height+InfoTile.Gap;
            }
            return result;
        }
    }
}
