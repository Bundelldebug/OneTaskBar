using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace OneTaskBar
{
    // Precision touchpads already send a continuous, inertial wheel stream. Preserve every
    // fractional delta; animate only discrete mouse-wheel notches, never touch manipulation.
    internal sealed class SmoothScrollViewer : ScrollViewer
    {
        static readonly DependencyProperty OffsetProperty=DependencyProperty.Register("Offset",typeof(double),typeof(SmoothScrollViewer),new PropertyMetadata(0.0,(s,e)=>((SmoothScrollViewer)s).ScrollToVerticalOffset((double)e.NewValue)));
        double target;bool animating;long lastPrecision;
        internal SmoothScrollViewer(){ScrollEdges.Attach(this);PanningMode=PanningMode.VerticalFirst;PanningDeceleration=.001;PanningRatio=1;CanContentScroll=false;Unloaded+=(s,e)=>Stop();PreviewTouchDown+=(s,e)=>Stop();PreviewMouseDown+=(s,e)=>Stop();}
        internal static double Distance(int delta,int lines,double viewport) {return -delta/120.0*(lines<0?viewport:Math.Max(0,lines)*16);}
        double Clamp(double offset){return Math.Max(0,Math.Min(ScrollableHeight,offset));}
        void Stop(){double current=VerticalOffset;if(animating){BeginAnimation(OffsetProperty,null);SetValue(OffsetProperty,current);}target=current;animating=false;lastPrecision=0;}
        internal void ResetForNavigation(double offset=0)
        {
            Stop();
            target=Math.Max(0,offset);
            SetValue(OffsetProperty,target);
            ScrollToVerticalOffset(target);
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if(e.Handled)return;if(ScrollableHeight<=0){base.OnMouseWheel(e);return;}
            double step=Distance(e.Delta,SystemParameters.WheelScrollLines,ViewportHeight);
            if(Math.Abs(e.Delta)<120||!Motion.Enabled){long now=System.Diagnostics.Stopwatch.GetTimestamp();bool continuous=!animating&&lastPrecision!=0&&(now-lastPrecision)/(double)System.Diagnostics.Stopwatch.Frequency<.14;if(animating)Stop();target=Clamp((continuous?target:VerticalOffset)+step);ScrollToVerticalOffset(target);lastPrecision=now;e.Handled=true;return;}
            double from=VerticalOffset;if(!animating||Math.Sign(step)!=Math.Sign(target-from))target=from;target=Clamp(target+step);
            BeginAnimation(OffsetProperty,null);SetValue(OffsetProperty,target);var motion=Motion.Tween(from,target,135);motion.Completed+=(s,args)=>animating=false;animating=true;BeginAnimation(OffsetProperty,motion);e.Handled=true;
        }
        protected override void OnKeyDown(KeyEventArgs e){Stop();base.OnKeyDown(e);}
        internal void CheckWheel(int delta){OnMouseWheel(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta){RoutedEvent=Mouse.MouseWheelEvent});}
    }
}
