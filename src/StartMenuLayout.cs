using System;
using System.Drawing;
namespace OneTaskBar
{
    internal sealed class StartMenuLayout
    {
        internal double X,Y,Width,Height;
        internal static StartMenuLayout Fit(Rectangle screen,Native.RectI work,double scale,double preferredHeight,int edge=3)
        {
            if(Double.IsNaN(scale)||Double.IsInfinity(scale)||scale<=0)scale=1;
            if(Double.IsNaN(preferredHeight)||Double.IsInfinity(preferredHeight))preferredHeight=620;
            var value=LauncherPlacement.Fit(screen,work,scale,940,Math.Max(620,Math.Ceiling(preferredHeight)),edge);
            return new StartMenuLayout {X=value.X,Y=value.Y,Width=value.Width,Height=value.Height};
        }
        internal static void Check()
        {
            LauncherPlacement.Check();
            foreach(double scale in new[]{1.0,1.25,1.5,1.75,2,2.5})foreach(var size in new[]{new Size(800,600),new Size(1366,768),new Size(1920,1080),new Size(2560,1440),new Size(2880,1800),new Size(3840,2160)})foreach(double preferred in new[]{620.0,700,820,1200}){
                var screen=new Rectangle(-2560,120,size.Width,size.Height);var work=new Native.RectI {Left=screen.Left+20,Top=screen.Top+24,Right=screen.Right,Bottom=screen.Bottom-68};var fit=Fit(screen,work,scale,preferred);
                if(fit.Width<=0||fit.Height<=0||fit.Width>940||fit.Height>Math.Max(620,preferred)||fit.X*scale+screen.Left<work.Left-.01||fit.Y*scale+screen.Top<work.Top-.01||(fit.X+fit.Width)*scale+screen.Left>work.Right+.01||(fit.Y+fit.Height)*scale+screen.Top>work.Bottom+.01)throw new InvalidOperationException("Start menu exceeds its monitor work area");
            }
            var large=Fit(new Rectangle(0,0,2880,1800),new Native.RectI {Right=2880,Bottom=1664},2,710);var small=Fit(new Rectangle(0,0,1280,720),new Native.RectI {Right=1280,Bottom=652},1,710);
            if(large.Height!=710||small.Height>=710||small.Y+small.Height>644.01)throw new InvalidOperationException("Start height does not adapt to content and display size");
        }
    }
}
