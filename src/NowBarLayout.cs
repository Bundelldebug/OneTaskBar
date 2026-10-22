using System;
using System.Windows;
namespace OneTaskBar
{
    internal sealed class NowBarLayout
    {
        internal Rect Bounds;internal double CompactY,CompactWidth;internal bool Raised;
        internal static NowBarLayout Fit(double screenWidth,double screenHeight,double startX,double dockY,double textWidth,bool expanded)
        {
            double gap=startX-24;bool raised=gap<132;double limit=Math.Max(1,Math.Min(DockLayout.StatusSize(screenWidth),raised?screenWidth-24:gap));double compact=Math.Min(limit,Math.Max(132,textWidth+64)),cy=Math.Max(12,raised?dockY-52:dockY);
            double width=expanded?Math.Max(1,Math.Min(400,screenWidth-24)):compact,height=expanded?Math.Min(188,Math.Max(100,cy-20)):44,y=expanded?Math.Max(12,cy-8-height):cy;
            return new NowBarLayout {Bounds=new Rect(12,y,width,height),CompactY=cy,CompactWidth=compact,Raised=raised};
        }
        internal static void Check()
        {
            foreach(double scale in new[]{1.0,1.5,2.0})foreach(int apps in new[]{2,12,28}){
                var physical=new System.Drawing.Rectangle(-1920,80,1920,1080);var dock=DockLayout.Compute(physical,scale,apps);
                foreach(bool expanded in new[]{false,true}){var fit=Fit(physical.Width/scale,physical.Height/scale,dock.StartX,dock.Y,1500,expanded);var b=fit.Bounds;
                    if(b.Left<0||b.Top<0||b.Right>physical.Width/scale||b.Bottom>physical.Height/scale||!expanded&&b.Width>dock.StatusWidth+.01)throw new Exception("Now bar exceeds monitor or status capsule bounds");
                    if(!expanded&&!fit.Raised&&b.Right>dock.StartX-11.99)throw new Exception("Now bar overlaps the existing dock");
                    if((expanded||fit.Raised)&&b.Bottom>dock.Y-7.99)throw new Exception("Raised Now bar overlaps taskbar");
                }
            }
            if(Fit(1440,900,400,844,25,false).Bounds.Width>=Fit(1440,900,400,844,180,false).Bounds.Width)throw new Exception("Now bar must fit the title width");
        }
    }
}
