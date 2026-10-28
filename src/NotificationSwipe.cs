using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OneTaskBar
{
    // Translate only the capsule while dragging; the list reflows after dismissal.
    internal sealed class NotificationSwipe
    {
        static readonly ConditionalWeakTable<FrameworkElement,NotificationSwipe> gestures=new ConditionalWeakTable<FrameworkElement,NotificationSwipe>();
        readonly FrameworkElement target;
        readonly Action dismiss;
        readonly Func<bool> allowed;
        readonly TranslateTransform shift=new TranslateTransform();
        readonly Stopwatch clock=new Stopwatch();
        Point origin;double lastX,lastAt,velocity;bool armed,dragging,settling,completed;
        internal bool Completed {get{return completed;}}
        internal double Offset {get{return shift.X;}}
        internal static NotificationSwipe Get(FrameworkElement target){NotificationSwipe value;return gestures.TryGetValue(target,out value)?value:null;}
        internal static NotificationSwipe Attach(FrameworkElement target,Action dismiss,Func<bool> allowed=null)
        {
            return gestures.GetValue(target,key=>new NotificationSwipe(key,dismiss,allowed??(()=>true)));
        }
        NotificationSwipe(FrameworkElement element,Action action,Func<bool> canSwipe)
        {
            target=element;dismiss=action;allowed=canSwipe;target.RenderTransform=shift;
            target.PreviewMouseLeftButtonDown+=Down;target.PreviewMouseMove+=Move;target.PreviewMouseLeftButtonUp+=Up;
            target.LostMouseCapture+=(s,e)=>{if(e.OriginalSource==target&&!target.IsMouseCaptured&&dragging&&!settling)Settle(false);};
            target.Unloaded+=(s,e)=>Reset();
            target.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Delete&&allowed()){e.Handled=true;Dismiss(1);}};
            var menu=new ContextMenu();var remove=new MenuItem {Header="알림 지우기"};remove.Click+=(s,e)=>Dismiss(1);menu.Items.Add(remove);target.ContextMenu=menu;
        }
        void Down(object sender,MouseButtonEventArgs e)
        {
            if(e.Handled||settling||completed||!allowed())return;
            origin=e.GetPosition(target.Parent as IInputElement);lastX=0;velocity=0;lastAt=0;armed=true;clock.Restart();
        }
        void Move(object sender,MouseEventArgs e)
        {
            if(!armed||e.LeftButton!=MouseButtonState.Pressed)return;
            var point=e.GetPosition(target.Parent as IInputElement);double dx=point.X-origin.X,dy=point.Y-origin.Y;
            if(!dragging)
            {
                if(Math.Abs(dy)>12&&Math.Abs(dy)>Math.Abs(dx)){armed=false;return;}
                if(Math.Abs(dx)<12||Math.Abs(dx)<Math.Abs(dy)*1.2)return;
                dragging=true;target.CaptureMouse();
            }
            double now=clock.Elapsed.TotalMilliseconds,elapsed=now-lastAt;
            if(elapsed>0)velocity=(dx-lastX)/elapsed;
            lastX=dx;lastAt=now;shift.X=dx;target.Opacity=Math.Max(.18,1-Math.Abs(dx)/Math.Max(1,target.ActualWidth)*.7);e.Handled=true;
        }
        internal static bool Commits(double distance,double velocity,double width)
        {
            return Math.Abs(distance)>=Math.Max(64,Math.Min(120,width*.28))||Math.Abs(distance)>=32&&Math.Abs(velocity)>.7&&Math.Sign(distance)==Math.Sign(velocity);
        }
        void Up(object sender,MouseButtonEventArgs e)
        {
            if(!armed)return;armed=false;
            if(!dragging){clock.Stop();return;}
            e.Handled=true;
            if(clock.Elapsed.TotalMilliseconds-lastAt>100)velocity=0;
            Settle(Commits(shift.X,velocity,target.ActualWidth));
        }
        internal void Dismiss(int direction)
        {
            if(settling||completed)return;
            if(Math.Abs(shift.X)<1)shift.X=direction<0?-1:1;
            Settle(true);
        }
        internal void Restore(){Reset();completed=false;}
        void Settle(bool remove)
        {
            settling=true;armed=dragging=false;clock.Stop();
            if(target.IsMouseCaptured)target.ReleaseMouseCapture();
            double from=shift.X,to=remove?Math.Sign(from)*Math.Max(100,target.ActualWidth+28):0;
            if(!Motion.Enabled||!target.IsVisible){if(remove){completed=true;dismiss();}else Reset();return;}
            var slide=Motion.Tween(from,to,remove?190:230);slide.FillBehavior=System.Windows.Media.Animation.FillBehavior.HoldEnd;
            var fade=Motion.Tween(target.Opacity,remove?0:1,remove?170:230);fade.FillBehavior=System.Windows.Media.Animation.FillBehavior.HoldEnd;
            slide.Completed+=(s,e)=>{if(remove){completed=true;dismiss();}else Reset();};
            shift.BeginAnimation(TranslateTransform.XProperty,slide);target.BeginAnimation(UIElement.OpacityProperty,fade);
        }
        void Reset()
        {
            armed=dragging=settling=false;clock.Stop();shift.BeginAnimation(TranslateTransform.XProperty,null);shift.X=0;target.BeginAnimation(UIElement.OpacityProperty,null);target.Opacity=1;
        }
    }
}
