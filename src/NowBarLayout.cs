using System;
using System.Windows;
namespace OneTaskBar
{
    internal sealed class NowBarLayout
    {
        internal Rect Bounds;internal double CompactY,CompactWidth,PosterEdge,PopupHeight;internal bool Raised;
        internal static NowBarLayout Fit(double screenWidth,double screenHeight,double startX,double dockY,double textWidth,bool expanded,bool largeCover=false,double maximumWidth=0,bool modern=false,int edge=3,double dockZoom=1)
        {
            double compactHeight=44*dockZoom,margin=12*dockZoom;
            if(DockGeometry.Vertical(edge))
            {
                double inset=80*dockZoom;
                double sideWidth=Math.Max(1,Math.Min(400,screenWidth-inset-24));
                double sideCover=largeCover?Math.Max(0,Math.Min(288,Math.Min(sideWidth-56,screenHeight-24-(modern?178:148)))):0;
                if(sideCover<120)sideCover=0;
                double sideHeight=sideCover>0?sideCover+(modern?178:148):Math.Min(modern?130:188,screenHeight-24);
                double sideX=expanded?(edge==0?inset:screenWidth-inset-sideWidth):(edge==0?margin:screenWidth-margin-compactHeight);
                double sideY=expanded?Math.Max(12,Math.Min(screenHeight-sideHeight-12,margin+compactHeight/2-sideHeight/2)):margin;
                return new NowBarLayout {Bounds=new Rect(sideX,sideY,expanded?sideWidth:compactHeight,expanded?sideHeight:compactHeight),CompactY=margin,CompactWidth=compactHeight,PosterEdge=sideCover,PopupHeight=sideHeight};
            }
            double gap=Math.Max(1,startX-24*dockZoom);
            double limit=Math.Max(1,Math.Min(maximumWidth>0?maximumWidth:DockLayout.StatusSize(screenWidth/dockZoom)*dockZoom,gap));
            double compact=limit<84*dockZoom?Math.Min(compactHeight,limit):Math.Min(limit,Math.Max(132*dockZoom,textWidth+64*dockZoom));
            double cy=Math.Max(margin,dockY),popupGap=8*dockZoom;
            double popupWidth=Math.Max(1,Math.Min(400,screenWidth-24));
            double cover=largeCover?Math.Max(0,Math.Min(288,Math.Min(popupWidth-56,cy-(modern?178:148)-popupGap-12))):0;
            if(cover<120)cover=0;
            double popupHeight=cover>0?cover+(modern?178:148):Math.Min(modern?130:188,Math.Max(100,cy-popupGap-12));
            double width=expanded?popupWidth:compact,height=expanded?popupHeight:compactHeight;
            double y=expanded?Math.Max(12,cy-popupGap-height):cy,left=expanded?12:margin;
            if(edge==1)y=screenHeight-y-height;
            return new NowBarLayout {Bounds=new Rect(left,y,width,height),CompactY=cy,CompactWidth=compact,Raised=false,PosterEdge=cover,PopupHeight=popupHeight};
        }
        internal static void Check()
        {
            CheckZoom();
            foreach(int edge in new[]{0,2})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5})foreach(bool modern in new[]{false,true})foreach(bool large in new[]{false,true})foreach(int count in new[]{0,4,40}){
                var monitor=new System.Drawing.Rectangle(-2560,-600,1920,1080);var dock=DockLayout.Compute(monitor,scale,count,58,true,68,edge);double w=monitor.Width/scale,h=monitor.Height/scale;var folded=Fit(w,h,dock.StartX,Math.Max(12,dock.StartX-56),1500,false,large,0,modern,edge);var opened=Fit(w,h,dock.StartX,Math.Max(12,dock.StartX-56),1500,true,large,0,modern,edge);var bounds=new Rect(0,0,w,h);
                if(folded.Bounds.Width!=44||folded.Bounds.Height!=44||Math.Abs(folded.Bounds.Top-12)>.01||Math.Abs(opened.Bounds.Top-12)>.01||!bounds.Contains(folded.Bounds)||!bounds.Contains(opened.Bounds)||folded.Bounds.Bottom>dock.StartX-11.99||(edge==0?Math.Abs(folded.Bounds.Left-12)>.01||opened.Bounds.Left<76:Math.Abs(w-folded.Bounds.Right-12)>.01||opened.Bounds.Right>w-76))throw new Exception("Side dock media must stay at the monitor top and expand inward");
            }
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
            foreach(double width in new[]{480.0,640,800,1024,1280,1920})foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5})foreach(int count in new[]{0,3,15,40})foreach(double power in new[]{0.0,58,78}){
                var area=new System.Drawing.Rectangle(-2560,-600,(int)(width*scale),1080);var dock=DockLayout.Compute(area,scale,count,power,true);var fit=Fit(width,area.Height/scale,dock.StartX,dock.Y,1500,false,false,dock.StatusWidth);
                if(fit.Raised||Math.Abs(fit.Bounds.Y-dock.Y)>.01||fit.Bounds.Right>dock.StartX-11.99||dock.AppX+dock.AppWidth+12>dock.StatusX+.01||fit.Bounds.Width>dock.StatusWidth+.01)throw new Exception("Small/mixed-DPI monitor media allocation");
            }
            foreach(double width in new[]{320.0,640,1440})foreach(double height in new[]{400.0,600,900}){var large=Fit(width,height,320,height-56,180,true,true);if(large.Bounds.Top<12||large.Bounds.Right>width||large.Bounds.Bottom>height-64||large.PosterEdge>288||large.PosterEdge>large.Bounds.Width-56)throw new Exception("Large cover exceeds monitor bounds");}
        }
        static void CheckZoom()
        {
            foreach(double zoom in new[]{.6,1,1.4})foreach(int edge in new[]{0,1,2,3})foreach(double dpi in new[]{1.0,1.25,1.5,2.0,2.5})foreach(int count in new[]{0,8,40})
            {
                var monitor=new System.Drawing.Rectangle(-2560,-600,1920,1080);
                var dock=DockLayout.Compute(monitor,dpi*zoom,count,58,true,68,edge);
                double width=monitor.Width/dpi,height=monitor.Height/dpi,top=dock.Y*zoom;
                var folded=Fit(width,height,dock.StartX*zoom,top,1500*zoom,false,false,0,true,edge,zoom).Bounds;
                var opened=Fit(width,height,dock.StartX*zoom,top,1500*zoom,true,false,0,true,edge,zoom).Bounds;
                var area=new Rect(0,0,width,height);
                if(!area.Contains(folded)||!area.Contains(opened)||Math.Abs(folded.Height-44*zoom)>.001||Math.Abs(opened.Height-130)>.001)
                    throw new Exception("Zoomed media geometry exceeds its monitor or changes the expanded player size");
                if(DockGeometry.Vertical(edge))
                {
                    if(Math.Abs(folded.Top-12*zoom)>.001||Math.Abs((edge==0?folded.Left:width-folded.Right)-12*zoom)>.001||Math.Abs((edge==0?opened.Left:width-opened.Right)-80*zoom)>.001)
                        throw new Exception("Zoomed side media does not follow its dock edge");
                }
                else
                {
                    double baseline=edge==1?height-top-folded.Height:top;
                    bool inward=edge==1?opened.Top>=folded.Bottom+8*zoom-.01:opened.Bottom<=folded.Top-8*zoom+.01;
                    if(Math.Abs(folded.Top-baseline)>.001||folded.Right>dock.StartX*zoom-12*zoom+.01||!inward)
                        throw new Exception("Zoomed horizontal media overlaps the app dock or opens outward");
                }
            }
        }
    }
}
