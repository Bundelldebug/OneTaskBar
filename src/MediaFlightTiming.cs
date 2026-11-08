using System;

namespace OneTaskBar
{
    internal static class MediaFlightTiming
    {
        internal const double MinimizeMilliseconds=480;
        internal const double RestoreMilliseconds=520;

        internal static double Smooth(double value)
        {
            double t=Math.Max(0,Math.Min(1,value));
            return t*t*t*(10+t*(-15+6*t));
        }

        const double Stiffness=8.5;
        // The critically damped step, rescaled so it lands exactly on 1 at the end of the flight.
        static double Spring(double x)
        {
            double raw=1-(1+Stiffness*x)*Math.Exp(-Stiffness*x);
            double end=1-(1+Stiffness)*Math.Exp(-Stiffness);
            return raw/end;
        }

        // A damped spring released from rest, the way the reference recordings move: quick off the start, then a
        // long soft landing. Both endpoints have zero velocity, and restoring plays the same curve backwards,
        // so a restore leaves the pill briskly and an interrupted flight turns around without a snap.
        internal static double Position(double elapsed,bool restoring)
        {
            double x=Math.Max(0,Math.Min(1,elapsed));
            if(x==0||x==1)return restoring?1-x:x;
            double value=Spring(x);
            return restoring?1-value:value;
        }
    }
}
