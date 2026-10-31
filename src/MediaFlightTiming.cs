using System;

namespace OneTaskBar
{
    internal static class MediaFlightTiming
    {
        internal const double MinimizeMilliseconds=460;
        internal const double RestoreMilliseconds=500;

        // Solve the time axis as well as the value axis. Both endpoints have
        // zero velocity; reversing a fast ease-out makes restoration snap shut.
        internal static double Position(double elapsed,bool restoring)
        {
            double x=Math.Max(0,Math.Min(1,elapsed));
            if(x==0||x==1)return restoring?1-x:x;
            double first=restoring?.22:.28;
            double second=restoring?.40:.45;
            double low=0,high=1,t=x;
            for(int i=0;i<16;i++)
            {
                double u=1-t;
                double time=3*u*u*t*first+3*u*t*t*second+t*t*t;
                if(time<x)low=t;
                else high=t;
                t=(low+high)/2;
            }
            double value=t*t*(3-2*t);
            return restoring?1-value:value;
        }
    }
}
