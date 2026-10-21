using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class BatteryAnimation
    {
        readonly BatteryPill pill;
        readonly DateTime started=DateTime.UtcNow;
        readonly EventHandler render;
        bool closed;
        internal readonly bool AllowDisconnected;
        internal BatteryAnimation(BatteryPill pill,bool preview=false)
        {
            this.pill=pill;AllowDisconnected=preview;pill.Celebration=true;pill.Phase=0;pill.Reveal=0;
            pill.BeginAnimation(BatteryPill.MorphProperty,Frames());
            render=(s,e)=>Frame(DateTime.UtcNow);CompositionTarget.Rendering+=render;
        }
        static DoubleAnimationUsingKeyFrames Frames()
        {
            var result=new DoubleAnimationUsingKeyFrames();
            result.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            result.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)),new KeySpline(.18,.8,.2,1)));
            result.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3300))));
            result.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(3800)),new KeySpline(.2,0,.18,1)));return result;
        }
        internal bool Frame(DateTime now)
        {
            if(closed)return false;double time=(now-started).TotalSeconds;pill.Phase=(time%1.6)/1.6;pill.Reveal=Math.Max(0,Math.Min(1,(time-.2)/.55));pill.InvalidateVisual();if(time>=3.85){Close();return false;}return true;
        }
        internal void Close(){if(closed)return;closed=true;CompositionTarget.Rendering-=render;pill.BeginAnimation(BatteryPill.MorphProperty,null);pill.Morph=0;pill.Celebration=false;pill.Reveal=1;pill.Phase=0;pill.InvalidateVisual();}
    }
}
