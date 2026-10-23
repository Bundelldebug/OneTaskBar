using System;
using System.Windows;
namespace OneTaskBar
{
    internal sealed class NowBarLayout
    {
        internal Rect Bounds;internal double CompactY,CompactWidth,PosterEdge,PopupHeight;internal bool Raised;
        internal static NowBarLayout Fit(double screenWidth,double screenHeight,double startX,double dockY,double textWidth,bool expanded,bool largeCover=false)
        {
            double gap=startX-24;bool raised=gap<132;double limit=Math.Max(1,Math.Min(DockLayout.StatusSize(screenWidth),raised?screenWidth-24:gap));double compact=Math.Min(limit,Math.Max(132,textWidth+64)),cy=Math.Max(12,raised?dockY-52:dockY);
            double popupWidth=Math.Max(1,Math.Min(400,screenWidth-24)),cover=largeCover?Math.Max(0,Math.Min(288,Math.Min(popupWidth-56,cy-168))):0;if(cover<120)cover=0;
            double popupHeight=cover>0?cover+148:Math.Min(188,Math.Max(100,cy-20)),width=expanded?popupWidth:compact,height=expanded?popupHeight:44,y=expanded?Math.Max(12,cy-8-height):cy;
            return new NowBarLayout {Bounds=new Rect(12,y,width,height),CompactY=cy,CompactWidth=compact,Raised=raised,PosterEdge=cover,PopupHeight=popupHeight};
        }
        internal static void Check()
        {
            foreach(double scale in new[]{1.0,1.5,2.0})foreach(int apps in new[]{2,12,28}){
                var physical=new System.Drawing.Rectangle(-1920,80,1920,1080);var dock=DockLayout.Compute(physical,scale,apps);
                foreach(bool expanded in new[]{false,true}){var fit=Fit(physical.Width/scale,physical.Height/scale,dock.StartX,dock.Y,1500,expanded);var b=fit.Bounds;
                    if(b.Left<0||b.Top<0||b.Right>physical.Width/scale||b.Bottom>physical.Height/scale||!expanded&&b.Width>dock.StatusWidth+.01)throw new Exception("미디어 플레이어 exceeds monitor or status capsule bounds");
                    if(!expanded&&!fit.Raised&&b.Right>dock.StartX-11.99)throw new Exception("미디어 플레이어 overlaps the existing dock");
                    if((expanded||fit.Raised)&&b.Bottom>dock.Y-7.99)throw new Exception("Raised 미디어 플레이어 overlaps taskbar");
                }
            }
            if(Fit(1440,900,400,844,25,false).Bounds.Width>=Fit(1440,900,400,844,180,false).Bounds.Width)throw new Exception("미디어 플레이어 must fit the title width");
            foreach(double width in new[]{320.0,640,1440})foreach(double height in new[]{400.0,600,900}){var large=Fit(width,height,320,height-56,180,true,true);if(large.Bounds.Top<12||large.Bounds.Right>width||large.Bounds.Bottom>height-64||large.PosterEdge>288||large.PosterEdge>large.Bounds.Width-56)throw new Exception("Large cover exceeds monitor bounds");}
        }
    }
}
