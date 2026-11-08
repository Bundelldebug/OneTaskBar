using System;
using System.Windows;

namespace OneTaskBar
{
    internal sealed class MediaWindowPose
    {
        internal Point Center;
        internal double Width,Height,Tilt,Turn,Opacity,Radius,Defocus;

        internal static MediaWindowPose At(Rect window,Rect capsule,double progress)
        {
            double p=Math.Max(0,Math.Min(1,progress)),u=1-p;
            var a=new Point(window.X+window.Width/2,window.Y+window.Height/2);
            var b=new Point(capsule.X+capsule.Width/2,capsule.Y+capsule.Height/2);
            var delta=b-a;
            // The window slides straight to the pill, the way an app folds into the Dynamic Island: no sideways swing.
            var c1=a+new Vector(delta.X*.32,delta.Y*.32);
            var c2=b-new Vector(delta.X*.18,delta.Y*.18);
            var center=new Point(u*u*u*a.X+3*u*u*p*c1.X+3*u*p*p*c2.X+p*p*p*b.X,
                u*u*u*a.Y+3*u*u*p*c1.Y+3*u*p*p*c2.Y+p*p*p*b.Y);
            // Cover the destination before rounding into its aspect. Both axes
            // keep shrinking; fitting first makes a wide capsule swell at the end.
            double end=Math.Max(capsule.Width/window.Width,capsule.Height/window.Height);
            double zoom=1+(end-1)*p;
            // The corners start rounding early, so the window is already pill-shaped as it reaches the pill.
            double morph=MediaFlightTiming.Smooth((p-.16)/.84);
            double width=window.Width*zoom-(window.Width*end-capsule.Width)*morph;
            double height=window.Height*zoom-(window.Height*end-capsule.Height)*morph;
            // The reference tilts hardest while the window is still small and next to the pill, then flattens as it opens.
            double envelope=Math.Min(1,1.35*Math.Pow(p,.8)*(1-Math.Pow(p,6)));
            double direction=delta.X>=0?1:-1;
            double radius=26+(Math.Min(width,height)/2-26)*morph;
            return new MediaWindowPose {
                Center=center,Width=width,Height=height,Radius=Math.Max(0,Math.Min(Math.Min(width,height)/2,radius)),
                Tilt=-4*direction*envelope,Turn=-2.5*direction*envelope,
                Opacity=Math.Max(0,1-MediaFlightTiming.Smooth((p-.78)/.22)),Defocus=Performance.Light?0:.3*envelope*envelope
            };
        }
    }

    internal static class MediaCapsulePose
    {
        internal static double HostPadding(Rect capsule)
        {
            return Math.Max(16, Math.Ceiling(Math.Max(capsule.Width, capsule.Height) * .075 / 2 + 4));
        }

        internal static Rect At(Rect capsule,Rect source,double progress)
        {
            double t=Math.Max(0,Math.Min(1,(progress-.50)/.50));
            double bump=64*t*t*t*(1-t)*(1-t)*(1-t);
            double stretch=1+.075*bump;
            bool horizontal=capsule.Width>=capsule.Height;
            double width=capsule.Width*(horizontal?stretch:1/stretch);
            double height=capsule.Height*(horizontal?1/stretch:stretch);
            var direction=new Vector(source.X+source.Width/2-capsule.X-capsule.Width/2,
                source.Y+source.Height/2-capsule.Y-capsule.Height/2);
            if(direction.LengthSquared>0)direction.Normalize();
            double nudge=2*bump;
            return new Rect(capsule.X+(capsule.Width-width)/2+direction.X*nudge,
                capsule.Y+(capsule.Height-height)/2+direction.Y*nudge,width,height);
        }
    }
}
