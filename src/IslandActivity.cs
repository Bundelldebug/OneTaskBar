using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    internal sealed class IslandActivity : StackPanel
    {
        readonly ScaleTransform[] bars=new ScaleTransform[4];
        readonly DoubleAnimation[] motion=new DoubleAnimation[4];
        bool playing;
        internal bool Active {get{return playing;}}

        internal IslandActivity()
        {
            Orientation=Orientation.Horizontal;
            HorizontalAlignment=HorizontalAlignment.Center;
            VerticalAlignment=VerticalAlignment.Center;
            IsHitTestVisible=false;
            for(int i=0;i<bars.Length;i++)
            {
                bars[i]=new ScaleTransform(1,.3+i*.12);
                var bar=new Border {Width=2.4,Height=14,Margin=new Thickness(1,0,1,0),CornerRadius=new CornerRadius(1.2),Background=Paint.ControlWhite,RenderTransformOrigin=new Point(.5,.5),RenderTransform=bars[i]};
                motion[i]=new DoubleAnimation(.25+i*.06,1,TimeSpan.FromMilliseconds(350+i*65)) {AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,BeginTime=TimeSpan.FromMilliseconds(i*80)};
                motion[i].Freeze();
                Children.Add(bar);
            }
            Unloaded+=(s,e)=>SetPlaying(false);
        }

        internal void SetPlaying(bool value)
        {
            if(playing==value)return;
            playing=value;
            for(int i=0;i<bars.Length;i++)
                bars[i].BeginAnimation(ScaleTransform.ScaleYProperty,value?motion[i]:null);
        }
    }
}
