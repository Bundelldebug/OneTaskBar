using System;
namespace OneTaskBar
{
    internal sealed class LauncherLayout
    {
        internal double X,Y,Width,Height;
        internal static LauncherLayout Fit(double width,double height)
        {
            double w=Math.Min(720,Math.Max(1,width-32)),h=Math.Min(680,Math.Max(1,height-100));
            return new LauncherLayout {X=(width-w)/2,Y=Math.Max(12,height-h-76),Width=w,Height=h};
        }
        internal static void Check()
        {
            foreach(var size in new[]{new[]{1440.0,900.0},new[]{960.0,600.0},new[]{320.0,480.0}}){var fit=Fit(size[0],size[1]);if(fit.Width>720||fit.Height>680||fit.X<0||fit.Y<0||fit.X+fit.Width>size[0]||fit.Y+fit.Height>size[1]-60)throw new Exception("Compact launcher exceeds monitor bounds");}
        }
    }
}
