using System;

namespace OneTaskBar
{
    internal static class MediaFlightTiming
    {
        // The Fluid UI's flight eases at both ends, so it needs no more time than the classic one to stay visible; the
        // owner found 620/640 ms too slow, then asked for it a little quicker still than 420/440.
        internal static double MinimizeMilliseconds {get{return Fluid.Enabled?360:480;}}
        internal static double RestoreMilliseconds {get{return Fluid.Enabled?380:520;}}

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
            if(Fluid.Enabled)
            {
                // The Fluid UI eases in and out, so the large window is seen leaving or arriving instead of most of the
                // move happening in the first frames. A restore swells a touch past its size near the end and settles,
                // like the island does.
                double value=Smooth(x);
                if(!restoring)return value;
                double swell=.05*Math.Sin(Math.PI*Math.Max(0,Math.Min(1,(x-.62)/.38)));
                return Math.Max(-.08,1-value-swell);
            }
            double spring=Spring(x);
            return restoring?1-spring:spring;
        }
    }
}
