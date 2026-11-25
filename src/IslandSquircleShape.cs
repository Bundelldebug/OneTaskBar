using System;
using System.Windows;
using System.Windows.Media;

namespace OneTaskBar
{
    // The outline of the squircle island: a rectangle whose corners follow a superellipse instead of a circle, so a
    // capsule keeps fuller, squarer ends like the pills in the owner's One UI clips. A whole-shape superellipse
    // (SquircleGeometry) would stretch on a wide pill, so only the corners are curved.
    internal static class IslandSquircleShape
    {
        // Well past a circle (2): flat sides that turn late and tight, as the pills and cards in the clips do. 3.2 was
        // tried first and read as an ordinary capsule.
        internal const double Exponent=5;
        const int Steps=16;
        static readonly double[] cosines=new double[Steps+1],sines=new double[Steps+1];

        static IslandSquircleShape()
        {
            double power=2/Exponent;
            for(int i=0;i<=Steps;i++)
            {
                double angle=i*Math.PI/2/Steps;
                cosines[i]=Math.Pow(Math.Cos(angle),power);
                sines[i]=Math.Pow(Math.Sin(angle),power);
            }
            cosines[Steps]=0;
            sines[0]=0;
        }

        internal static Geometry Create(Rect bounds,double radius)
        {
            radius=Math.Max(0,Math.Min(radius,Math.Min(bounds.Width,bounds.Height)/2));
            var shape=new StreamGeometry();
            using(var context=shape.Open())
            {
                double left=bounds.Left,top=bounds.Top,right=bounds.Right,bottom=bounds.Bottom;
                context.BeginFigure(new Point(left+radius,top),true,true);
                // Each corner runs from the end of one straight side to the start of the next.
                Corner(context,right-radius,top+radius,radius,1,-1,true);
                Corner(context,right-radius,bottom-radius,radius,1,1,false);
                Corner(context,left+radius,bottom-radius,radius,-1,1,true);
                Corner(context,left+radius,top+radius,radius,-1,-1,false);
            }
            shape.Freeze();
            return shape;
        }

        // A quarter of the superellipse around (cx, cy). The order flips on alternate corners so the outline runs
        // clockwise without crossing itself.
        static void Corner(StreamGeometryContext context,double cx,double cy,double radius,int sx,int sy,bool fromTop)
        {
            for(int step=0;step<=Steps;step++)
            {
                int i=fromTop?Steps-step:step;
                context.LineTo(new Point(cx+sx*radius*cosines[i],cy+sy*radius*sines[i]),true,true);
            }
        }
    }
}
