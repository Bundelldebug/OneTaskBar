using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // The position of a damped spring released from rest, stretched over an animation's duration. This is the
    // motion Apple's systems use: quick to leave, then a long, soft arrival. A damping ratio of 1 never
    // overshoots; lower ratios overshoot a little and settle, which is what gives presses and pop-ins their life.
    // Every timed tween, WPF animation or hand-driven frame loop shares this one curve.
    internal sealed class SpringEase : EasingFunctionBase
    {
        // Critically damped: the default for anything that moves, fades or resizes.
        internal static readonly SpringEase Smooth=Frozen(1);
        // A touch of overshoot (about 1%): buttons, toggles and pills landing in place.
        internal static readonly SpringEase Snappy=Frozen(.86);
        // A visible, lively settle (about 4%): launches and pops.
        internal static readonly SpringEase Bouncy=Frozen(.72);
        // The Fluid UI's pop: a clear stretch past the target (about 12%) that settles like liquid.
        internal static readonly SpringEase Liquid=Frozen(.78);
        readonly double damping;
        static SpringEase Frozen(double damping){var ease=new SpringEase(damping);ease.Freeze();return ease;}
        public SpringEase():this(1){}
        internal SpringEase(double damping)
        {
            this.damping=damping;EasingMode=EasingMode.EaseIn;
        }
        protected override Freezable CreateInstanceCore(){return new SpringEase(damping);}
        protected override double EaseInCore(double normalizedTime){return Position(normalizedTime,damping);}
        // 0 at the start, 1 at the end; the envelope has decayed to under 0.1% by the end of the duration.
        internal static double Position(double progress,double damping)
        {
            if(progress<=0)return 0;
            if(progress>=1)return 1;
            if(damping>=1)
            {
                double k=9*progress;
                return 1-(1+k)*Math.Exp(-k);
            }
            double decay=7,turn=decay/damping*Math.Sqrt(1-damping*damping),angle=turn*progress;
            return 1-Math.Exp(-decay*progress)*(Math.Cos(angle)+decay/turn*Math.Sin(angle));
        }
        // For frame loops: the same curve without building an easing object.
        internal static double Out(double progress){return Position(progress,1);}
        internal static double Settle(double progress){return Position(progress,.86);}
    }
}
