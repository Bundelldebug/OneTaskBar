using System;
using System.Drawing;
using System.Windows;

namespace OneTaskBar
{
    internal static class LauncherPlacement
    {
        // Coordinates stay relative to the selected monitor, in WPF DIPs.
        internal static Rect Fit(Rectangle screen,Native.RectI work,double scale,double maximumWidth,double preferredHeight,int edge)
        {
            if(Double.IsNaN(scale)||Double.IsInfinity(scale)||scale<=0)scale=1;
            double sw=screen.Width/scale,sh=screen.Height/scale;
            double left=Math.Max(0,(work.Left-screen.Left)/scale),top=Math.Max(0,(work.Top-screen.Top)/scale);
            double right=Math.Min(sw,(work.Right-screen.Left)/scale),bottom=Math.Min(sh,(work.Bottom-screen.Top)/scale);
            if(right<=left||bottom<=top){left=top=0;right=sw;bottom=sh;}
            if(edge==0)left=Math.Max(left,DockSizing.PopupInset);
            else if(edge==1)top=Math.Max(top,DockSizing.PopupInset);
            else if(edge==2)right=Math.Min(right,sw-DockSizing.PopupInset);
            else bottom=Math.Min(bottom,sh-DockSizing.Reserved);
            double insetX=Math.Min(16,Math.Max(0,(right-left)/8)),insetY=Math.Min(8,Math.Max(0,(bottom-top)/8));
            left+=insetX;right-=insetX;top+=insetY;bottom-=insetY;
            double width=Math.Min(maximumWidth,Math.Max(1,right-left)),height=Math.Min(preferredHeight,Math.Max(1,bottom-top));
            double x=edge==0?left:edge==2?right-width:left+(right-left-width)/2;
            double y=edge==1?top:edge==0||edge==2?top+(bottom-top-height)/2:bottom-height;
            return new Rect(x,y,width,height);
        }
        internal static void Check()
        {
            foreach(double scale in new[]{1.0,1.25,1.5,2,2.5})
            foreach(int edge in new[]{0,1,2,3})
            {
                var screen=new Rectangle(-2560,-360,1920,1080);
                var work=new Native.RectI {Left=screen.Left,Top=screen.Top,Right=screen.Right,Bottom=screen.Bottom};
                var value=Fit(screen,work,scale,940,710,edge);
                if(value.Left<0||value.Top<0||value.Right>screen.Width/scale+.01||value.Bottom>screen.Height/scale+.01)throw new InvalidOperationException("Launcher placement exceeds its monitor");
                bool reserved=edge==0?value.Left>=76:edge==1?value.Top>=76:edge==2?value.Right<=screen.Width/scale-76:value.Bottom<=screen.Height/scale-68;
                if(!reserved)throw new InvalidOperationException("Launcher overlaps the dock edge");
            }
        }
    }
}
