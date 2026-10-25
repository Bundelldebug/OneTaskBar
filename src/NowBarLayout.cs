using System;
using System.Windows;
namespace OneTaskBar
{
    internal sealed class NowBarLayout
    {
        internal Rect Bounds;internal double CompactY,CompactWidth,PosterEdge,PopupHeight;internal bool Raised;
        internal static NowBarLayout Fit(double screenWidth,double screenHeight,double startX,double dockY,double textWidth,bool expanded,bool largeCover=false,double maximumWidth=0,bool modern=false)
        {
            double gap=Math.Max(1,startX-24),limit=Math.Max(1,Math.Min(maximumWidth>0?maximumWidth:DockLayout.StatusSize(screenWidth),gap)),compact=limit<84?Math.Min(44,limit):Math.Min(limit,Math.Max(132,textWidth+64)),cy=Math.Max(12,dockY);
            double popupWidth=Math.Max(1,Math.Min(400,screenWidth-24)),cover=largeCover?Math.Max(0,Math.Min(288,Math.Min(popupWidth-56,cy-(modern?198:168)))):0;if(cover<120)cover=0;
            double popupHeight=cover>0?cover+(modern?178:148):Math.Min(modern?130:188,Math.Max(100,cy-20)),width=expanded?popupWidth:compact,height=expanded?popupHeight:44,y=expanded?Math.Max(12,cy-8-height):cy;
            return new NowBarLayout {Bounds=new Rect(12,y,width,height),CompactY=cy,CompactWidth=compact,Raised=false,PosterEdge=cover,PopupHeight=popupHeight};
        }
        internal static void Check()
        {
            foreach(bool cover in new[]{false,true})foreach(double width in new[]{320.0,640,1440})foreach(double height in new[]{400.0,600,900}){var fit=Fit(width,height,320,height-56,180,true,cover,0,true);if(fit.Bounds.Top<12||fit.Bounds.Bottom>height-64||fit.Bounds.Right>width||(!cover&&height>=400&&fit.PopupHeight!=130))throw new Exception("UI 9 player spacing exceeds its monitor");}
            foreach(double scale in new[]{1.0,1.5,2.0})foreach(int apps in new[]{2,12,28}){
                var physical=new System.Drawing.Rectangle(-1920,80,1920,1080);var dock=DockLayout.Compute(physical,scale,apps,58,true);
                foreach(bool expanded in new[]{false,true}){var fit=Fit(physical.Width/scale,physical.Height/scale,dock.StartX,dock.Y,1500,expanded);var b=fit.Bounds;
                    if(b.Left<0||b.Top<0||b.Right>physical.Width/scale||b.Bottom>physical.Height/scale||!expanded&&b.Width>dock.StatusWidth+.01)throw new Exception("미디어 플레이어 exceeds monitor or status capsule bounds");
                    if(!expanded&&(b.Right>dock.StartX-11.99||Math.Abs(b.Y-dock.Y)>.01||fit.Raised))throw new Exception("미디어 플레이어 overlaps or leaves the dock baseline");
                    if(expanded&&b.Bottom>dock.Y-7.99)throw new Exception("Expanded 미디어 플레이어 overlaps taskbar");
                }
            }
            if(Fit(1440,900,400,844,25,false).Bounds.Width>=Fit(1440,900,400,844,180,false).Bounds.Width)throw new Exception("미디어 플레이어 must fit the title width");
            foreach(double width in new[]{480.0,640,800,1024,1280,1920})foreach(double scale in new[]{1.0,1.25,1.5,2.0,2.5})foreach(int count in new[]{0,3,15,40})foreach(double power in new[]{0.0,58,78}){
                var area=new System.Drawing.Rectangle(-2560,-600,(int)(width*scale),1080);var dock=DockLayout.Compute(area,scale,count,power,true);var fit=Fit(width,area.Height/scale,dock.StartX,dock.Y,1500,false,false,dock.StatusWidth);
                if(fit.Raised||Math.Abs(fit.Bounds.Y-dock.Y)>.01||fit.Bounds.Right>dock.StartX-11.99||dock.AppX+dock.AppWidth+12>dock.StatusX+.01||fit.Bounds.Width>dock.StatusWidth+.01)throw new Exception("Small/mixed-DPI monitor media allocation");
            }
            foreach(double width in new[]{320.0,640,1440})foreach(double height in new[]{400.0,600,900}){var large=Fit(width,height,320,height-56,180,true,true);if(large.Bounds.Top<12||large.Bounds.Right>width||large.Bounds.Bottom>height-64||large.PosterEdge>288||large.PosterEdge>large.Bounds.Width-56)throw new Exception("Large cover exceeds monitor bounds");}
        }
    }
}
