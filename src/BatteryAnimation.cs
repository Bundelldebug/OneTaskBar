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
        readonly double endSeconds;
        bool closed;
        internal readonly bool AllowDisconnected;
        internal bool Active {get{return !closed;}}
        internal Action Completed;
        internal BatteryAnimation(BatteryPill pill,bool preview=false,int holdMs=3300,int endMs=3800)
        {
            this.pill=pill;AllowDisconnected=preview;endSeconds=(endMs+50)/1000.0;pill.Celebration=true;pill.Phase=0;pill.Reveal=0;
            pill.BeginAnimation(BatteryPill.MorphProperty,Frames(holdMs,endMs));
            render=(s,e)=>Frame(DateTime.UtcNow);CompositionTarget.Rendering+=render;
        }
        static DoubleAnimationUsingKeyFrames Frames(int holdMs,int endMs)
        {
            var result=new DoubleAnimationUsingKeyFrames();
            result.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));
            result.KeyFrames.Add(new SplineDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(270)),new KeySpline(.18,.8,.2,1)));
            result.KeyFrames.Add(new DiscreteDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(holdMs))));
            result.KeyFrames.Add(new SplineDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(endMs)),new KeySpline(.2,0,.18,1)));return result;
        }
        internal bool Frame(DateTime now)
        {
            if(closed)return false;double time=(now-started).TotalSeconds;pill.Phase=(time%1.6)/1.6;pill.Reveal=Math.Max(0,Math.Min(1,(time-.2)/.55));pill.InvalidateVisual();if(time>=endSeconds){Close();return false;}return true;
        }
        internal void Close(){if(closed)return;closed=true;CompositionTarget.Rendering-=render;pill.BeginAnimation(BatteryPill.MorphProperty,null);pill.Morph=0;pill.Celebration=false;pill.Reveal=1;pill.Phase=0;pill.InvalidateVisual();var done=Completed;Completed=null;if(done!=null)done();}
    }
}
