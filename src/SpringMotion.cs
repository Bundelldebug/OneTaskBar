using System;
using System.Windows;

namespace OneTaskBar
{
    // A critically damped spring. Unlike a timed curve it keeps its speed when the target changes
    // mid-flight, so reversing an animation bends smoothly instead of stopping and restarting.
    // The step uses the exact solution, so it is the same at 60 and 144 frames a second.
    internal sealed class SpringValue
    {
        // About 0.3 seconds to settle: quick enough to feel direct, soft enough to feel continuous.
        internal double Omega=28;
        internal double Value,Velocity,Target,Epsilon=.1;
        internal SpringValue(double epsilon=.1)
        {
            Epsilon=epsilon;
        }
        internal bool AtRest {get{return Math.Abs(Target-Value)<Epsilon&&Math.Abs(Velocity)<Epsilon*120;}}
        internal void Snap()
        {
            Value=Target;
            Velocity=0;
        }
        internal void Step(double seconds)
        {
            double offset=Value-Target,push=Velocity+Omega*offset,decay=Math.Exp(-Omega*seconds);
            Value=Target+(offset+push*seconds)*decay;
            Velocity=(Velocity-push*Omega*seconds)*decay;
        }
        static double[] Vector(Rect from,Rect to)
        {
            return new[]{to.X-from.X,to.Y-from.Y,to.Width-from.Width,to.Height-from.Height};
        }
        // Starts a slide from where a shape is to a new target. If it was already sliding, the speed it has
        // in the direction it now needs carries over, so a reversal bends instead of starting from rest.
        internal static void Handoff(SpringValue slide,bool moving,Rect current,Rect from,Rect oldTarget,Rect newTarget)
        {
            double carried=0;
            var next=Vector(current,newTarget);
            double length=next[0]*next[0]+next[1]*next[1]+next[2]*next[2]+next[3]*next[3];
            if(moving&&length>.01)
            {
                var old=Vector(from,oldTarget);
                carried=slide.Velocity*(old[0]*next[0]+old[1]*next[1]+old[2]*next[2]+old[3]*next[3])/length;
            }
            slide.Value=0;
            slide.Target=1;
            slide.Velocity=carried;
        }
    }

    // Time for a spring. The first step counts from the moment the spring was aimed, so a frame that
    // arrives late, such as while a panel is being built, does not make the motion start late.
    // After that the steps follow the frame times, which keeps them even.
    internal sealed class SpringClock
    {
        TimeSpan last=TimeSpan.MinValue;
        long started=System.Diagnostics.Stopwatch.GetTimestamp();
        internal void Restart()
        {
            last=TimeSpan.MinValue;
            started=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        internal double Next(TimeSpan frame)
        {
            double elapsed;
            if(last==TimeSpan.MinValue)elapsed=(System.Diagnostics.Stopwatch.GetTimestamp()-started)/(double)System.Diagnostics.Stopwatch.Frequency;
            else elapsed=(frame-last).TotalSeconds;
            last=frame;
            // The step is exact for any length of time, so a stalled frame catches up as wall-clock progress would.
            return Math.Max(0.001,Math.Min(.5,elapsed));
        }
    }
}
